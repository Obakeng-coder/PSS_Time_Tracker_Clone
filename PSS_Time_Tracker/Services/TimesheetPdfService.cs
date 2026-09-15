using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using PSS_Time_Tracker;
using TimeSheetRecorder;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Single implementation of the branded weekly-timesheet PDF, shared by the employee-facing
    /// "Generate Weekly Report" screen and the manager-facing "Generate Employee Weekly Report" screen.
    /// This used to be ~250 lines duplicated in each controller (see git history) — anything that changes
    /// the report's layout now only needs to change here.
    /// </summary>
    public class TimesheetPdfService : ITimesheetPdfService
    {
        private const float BaseFontSize = 10f;
        private const float HeaderFontSize = 11f;
        private const float SignatureFontSize = 10f;

        private readonly IWebHostEnvironment _hostingEnvironment;

        public TimesheetPdfService(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public byte[] GenerateWeeklyReport(WeeklyReportPdfRequest request)
        {
            var dayRows = request.DayRows;

            var totalHours = dayRows.Sum(r => r.Hours);

            // Distinct work locations across the week (pulled from SharePoint - one per entry, no
            // parsing needed now that a day only ever has a single location). Leave/holiday/gap rows
            // have no WorkLocation of their own, so they're naturally excluded here.
            string workLocations = string.Join(", ",
                dayRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.WorkLocation))
                    .Select(r => r.WorkLocation!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));

            // Load the Whisper font for the signature; fall back to italic Helvetica if it's missing.
            string fontPath = Path.Combine(_hostingEnvironment.WebRootPath, "Whisper-Regular.ttf");
            Font signatureFont;
            if (System.IO.File.Exists(fontPath))
            {
                var signatureBaseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                signatureFont = new Font(signatureBaseFont, 15f);
            }
            else
            {
                signatureFont = FontFactory.GetFont(FontFactory.HELVETICA, 10f, Font.ITALIC);
            }

            using var ms = new MemoryStream();
            var document = new Document(PageSize.A4, 25, 25, 110, 50);
            var writer = PdfWriter.GetInstance(document, ms);

            var headerPath = "assets/images/Banner.png";
            var footerPath = "assets/images/Footer.png";
            writer.PageEvent = new HeaderFooter(_hostingEnvironment, headerPath, footerPath);

            document.Open();

            // --- Employee info table ---
            var employeeTable = new PdfPTable(4) { WidthPercentage = 100 };
            employeeTable.SetWidths(new float[] { 20, 30, 20, 30 });

            AddRedLabelCell(employeeTable, "Employee Name");
            AddWhiteValueCell(employeeTable, $"{request.EmployeeName} {request.EmployeeSurname}");
            AddRedLabelCell(employeeTable, "ID Number");
            AddWhiteValueCell(employeeTable, "");
            AddRedLabelCell(employeeTable, "Job Title");
            AddWhiteValueCell(employeeTable, string.IsNullOrWhiteSpace(request.JobTitle) ? "" : request.JobTitle);
            AddRedLabelCell(employeeTable, "Employment Type");
            AddWhiteValueCell(employeeTable, "");
            AddRedLabelCell(employeeTable, "Supervisor");
            AddWhiteValueCell(employeeTable, request.SupervisorFullName ?? ".");
            AddRedLabelCell(employeeTable, "Work Location");
            AddWhiteValueCell(employeeTable, workLocations);

            document.Add(employeeTable);
            document.Add(new Paragraph(" ") { SpacingAfter = 10 });

            // --- Month banner ---
            string monthName = request.StartDate.ToString("MMMM yyyy");
            var monthTable = new PdfPTable(2)
            {
                WidthPercentage = 70,
                HorizontalAlignment = Element.ALIGN_CENTER
            };
            monthTable.SetWidths(new float[] { 60, 40 });

            var monthLabelCell = new PdfPCell(new Phrase("Weekly Time Sheet For the Month Of",
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)))
            {
                BackgroundColor = new BaseColor(255, 0, 0),
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 6,
                BorderWidthRight = 0
            };
            monthTable.AddCell(monthLabelCell);

            var monthValueCell = new PdfPCell(new Phrase(monthName,
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 6,
                BorderWidthLeft = 0
            };
            monthTable.AddCell(monthValueCell);

            document.Add(monthTable);
            document.Add(new Paragraph(" ") { SpacingAfter = 10 });

            // --- Daily entries table ---
            var timesheetTable = new PdfPTable(7) { WidthPercentage = 100 };
            timesheetTable.SetWidths(new float[] { 8, 12, 25, 12, 12, 12, 18 });

            AddRedHeaderCell(timesheetTable, "No");
            AddRedHeaderCell(timesheetTable, "Date");
            AddRedHeaderCell(timesheetTable, "Daily Task");
            AddRedHeaderCell(timesheetTable, "Start Time");
            AddRedHeaderCell(timesheetTable, "End Time");
            AddRedHeaderCell(timesheetTable, "Total Hours");
            AddRedHeaderCell(timesheetTable, "Signature");

            int entryNumber = 1;
            foreach (var row in dayRows)
            {
                AddTimesheetDataCell(timesheetTable, entryNumber.ToString());
                AddTimesheetDataCell(timesheetTable, row.Date.ToString("yyyy-MM-dd"));

                // TaskDescription already carries the right primary text for every row kind (the real
                // daily task for a worked day, "Approved Leave - <Type>" for leave, "Public Holiday" for
                // an unworked holiday, the manager's own notes - or a generic method label if they left
                // notes blank - for a resolved gap). Note adds a short annotation on top when there's
                // something worth flagging beyond that (paid/unpaid, overtime, standard-pay) - appended
                // rather than replacing the primary text, so a resolved gap's actual notes never get
                // silently swapped out for a generic label.
                string taskText = row.TaskDescription;
                if (!string.IsNullOrWhiteSpace(row.Note) &&
                    (string.IsNullOrWhiteSpace(taskText) || !taskText.Contains(row.Note, StringComparison.OrdinalIgnoreCase)))
                {
                    taskText = string.IsNullOrWhiteSpace(taskText) ? row.Note : $"{taskText} ({row.Note})";
                }

                string cleanTask = Regex.Replace(taskText, @"\s+", " ").Trim();
                string truncatedTask = cleanTask.Length > 45 ? cleanTask.Substring(0, 45) + "..." : cleanTask;

                var taskCell = new PdfPCell(new Phrase(truncatedTask, FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)))
                {
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    Padding = 15
                };

                // Long tasks get the full text attached as a PDF comment annotation instead of being
                // silently cut off.
                if (cleanTask.Length > 45)
                {
                    var annotation = PdfAnnotation.CreateText(writer, null, "Full Task:\n", cleanTask, false, "Comment");
                    annotation.Put(PdfName.C, new PdfArray(new float[] { 1f, 0.5f, 0f }));
                    annotation.Put(PdfName.OPEN, new PdfBoolean(false));
                    taskCell.CellEvent = new AnnotatedCellEvent(writer, annotation);
                }

                timesheetTable.AddCell(taskCell);

                if (row.StartTime.HasValue && row.EndTime.HasValue)
                {
                    AddTimesheetDataCell(timesheetTable, row.StartTime.Value.ToString("HH:mm"));
                    AddTimesheetDataCell(timesheetTable, row.EndTime.Value.ToString("HH:mm"));
                    AddTimesheetDataCell(timesheetTable, FormatHours(row.Hours));
                }
                else if (row.Hours > 0)
                {
                    // Leave/holiday-off/gap-resolved rows have no clock-in times, but still carry hours
                    // for the total.
                    AddTimesheetDataCell(timesheetTable, "");
                    AddTimesheetDataCell(timesheetTable, "");
                    AddTimesheetDataCell(timesheetTable, FormatHours(row.Hours));
                }
                else
                {
                    AddTimesheetDataCell(timesheetTable, "");
                    AddTimesheetDataCell(timesheetTable, "");
                    AddTimesheetDataCell(timesheetTable, "");
                }

                AddSignatureDataCell(timesheetTable, row.SignatureDisplay, signatureFont);
                entryNumber++;
            }

            document.Add(timesheetTable);

            // --- Total hours ---
            var totalHoursTable = new PdfPTable(2)
            {
                WidthPercentage = 70,
                HorizontalAlignment = Element.ALIGN_CENTER
            };
            totalHoursTable.SetWidths(new float[] { 50, 50 });

            var totalLabelCell = new PdfPCell(new Phrase("Total Working Hours",
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)))
            {
                BackgroundColor = new BaseColor(255, 0, 0),
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 8,
                BorderWidthRight = 0
            };
            totalHoursTable.AddCell(totalLabelCell);

            var totalValueCell = new PdfPCell(new Phrase(FormatHours(totalHours),
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 6,
                BorderWidthLeft = 0
            };
            totalHoursTable.AddCell(totalValueCell);

            document.Add(new Paragraph(" ") { SpacingBefore = 8 });
            document.Add(totalHoursTable);
            document.Add(new Paragraph(" ") { SpacingBefore = 15 });

            // --- Signatures ---
            var signatureTable = new PdfPTable(4) { WidthPercentage = 100 };
            signatureTable.SetWidths(new float[] { 20, 30, 20, 30 });

            AddRedLabelCell(signatureTable, "Employee Signature");
            AddWhiteSignatureCell(signatureTable, request.Signature, signatureFont);
            AddRedLabelCell(signatureTable, "Supervisor Signature");
            AddWhiteSignatureCell(signatureTable, request.SupervisorSignature, signatureFont);

            AddRedLabelCell(signatureTable, "Date");
            AddWhiteSignatureCell(signatureTable,
                string.IsNullOrEmpty(request.Signature) ? "" : (request.SignatureDate ?? request.EndDate).ToString("yyyy-MM-dd"),
                signatureFont);
            AddRedLabelCell(signatureTable, "Date");
            AddWhiteSignatureCell(signatureTable,
                string.IsNullOrEmpty(request.SupervisorSignature) ? "" : (request.SupervisorSignatureDate ?? request.EndDate).ToString("yyyy-MM-dd"),
                signatureFont);

            document.Add(signatureTable);

            // --- Comments ---
            var commentsTable = new PdfPTable(2) { WidthPercentage = 100 };
            commentsTable.SetWidths(new float[] { 20, 80 });

            AddRedLabelCell(commentsTable, "Comments");
            AddWhiteCommentsCell(commentsTable, "");

            document.Add(commentsTable);

            document.Close();

            return ms.ToArray();
        }

        private static void AddSignatureDataCell(PdfPTable table, string text, Font signatureFont)
        {
            var cell = new PdfPCell(new Phrase(text, signatureFont))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 10,
                MinimumHeight = 30
            };
            table.AddCell(cell);
        }

        private static void AddRedLabelCell(PdfPTable table, string text)
        {
            var cell = new PdfPCell(new Phrase(text,
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)))
            {
                BackgroundColor = new BaseColor(255, 0, 0),
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 5
            };
            table.AddCell(cell);
        }

        private static void AddWhiteValueCell(PdfPTable table, string text)
        {
            var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 5,
                BorderWidthLeft = 0
            };
            table.AddCell(cell);
        }

        private static void AddRedHeaderCell(PdfPTable table, string text)
        {
            var cell = new PdfPCell(new Phrase(text,
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)))
            {
                BackgroundColor = new BaseColor(255, 0, 0),
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 5
            };
            table.AddCell(cell);
        }

        private static void AddTimesheetDataCell(PdfPTable table, string text)
        {
            table.AddCell(new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)))
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 5
            });
        }

        private static void AddWhiteSignatureCell(PdfPTable table, string text, Font signatureFont)
        {
            var cell = new PdfPCell(new Phrase(text, signatureFont))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 10,
                MinimumHeight = 30,
                BorderWidthLeft = 0
            };
            table.AddCell(cell);
        }

        private static void AddWhiteCommentsCell(PdfPTable table, string text)
        {
            var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_TOP,
                Padding = 8,
                MinimumHeight = 50,
                BorderWidthLeft = 0
            };
            table.AddCell(cell);
        }

        // Converts decimal hours (e.g., 7.63) to "7 Hrs 38 Mins"
        private static string FormatHours(double totalHours)
        {
            int hours = (int)totalHours;
            int minutes = (int)Math.Round((totalHours - hours) * 60);
            return $"{hours} Hrs {minutes:D2} Mins";
        }
    }
}
