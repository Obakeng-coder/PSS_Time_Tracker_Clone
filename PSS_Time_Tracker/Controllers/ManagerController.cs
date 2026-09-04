using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TimeSheetRecorder;


namespace PSS_Time_Tracker.Controllers
{

    public class ManagerController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ManagerController(timeSheetRecorderContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }



        public async Task<IActionResult> Index(int page = 1, string searchTerm = null)
        {

            int pageSize = 10;

            var managerEmail = User.FindFirst("preferred_username")?.Value
                       ?? User.FindFirst("email")?.Value;

            List<EmployeeSummaryResult> employeeGroups;
            int totalRecords;

            employeeGroups = await _context.EmployeeSummaryResults
                .FromSqlInterpolated($@"
        EXEC GetManagerEmployeeTimeSheetsPaged 
            @SupervisorEmail={managerEmail}, 
            @StartDate={null}, 
            @EndDate={null}, 
            @PageNumber={page}, 
            @PageSize={pageSize}")
                .AsNoTracking()
                .ToListAsync();

            totalRecords = _context.SpResults
                .FromSqlInterpolated($@"
        EXEC GetManagerEmployeeTimeSheetsCount 
            @SupervisorEmail={managerEmail}, 
            @StartDate={null}, 
            @EndDate={null}")
                .AsNoTracking()
                .AsEnumerable()
                .Select(r => r.TotalCount)
                .FirstOrDefault();




            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            var employees = employeeGroups.Select(g => new EmployeeViewModel
            {
                AzureAdUserId = g.AzureAdUserId,
                FullName = $"{g.EmployeeName} {g.EmployeeSurname}",
                TimesheetCount = g.TimesheetCount,
                LatestEntry = g.LatestEntry
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < totalPages;
            ViewBag.SearchTerm = searchTerm;

            return View(employees);
        }



        public async Task<IActionResult> EmployeeTimeSheetDetails(string id, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            int pageSize = 10;

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var employeeIdParam = new SqlParameter("@EmployeeId", id);
            var startDateParam = new SqlParameter("@StartDate", (object?)startDate ?? DBNull.Value);
            var endDateParam = new SqlParameter("@EndDate", (object?)endDate ?? DBNull.Value);
            var pageNumberParam = new SqlParameter("@PageNumber", page);
            var pageSizeParam = new SqlParameter("@PageSize", pageSize);

       
            var timesheetEntries = await _context.TimeTracker
                .FromSqlRaw("EXEC GetEmployeeTimesheetPaged @EmployeeId, @StartDate, @EndDate, @PageNumber, @PageSize",
                    employeeIdParam, startDateParam, endDateParam, pageNumberParam, pageSizeParam)
                .ToListAsync();

           
            var totalEntries = await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"EXEC GetEmployeeTimesheetCount @EmployeeId={id}, @StartDate={startDate}, @EndDate={endDate}");

            var totalPages = (int)Math.Ceiling(totalEntries / (double)pageSize);
            var hasPrevious = page > 1;
            var hasNext = page < totalPages;

          
            var employee = timesheetEntries.FirstOrDefault();
            var fullName = employee != null
                ? $"{employee.EmployeeName} {employee.EmployeeSurname}"
                : "Unknown";

           
            ViewBag.EmployeeId = id;
            ViewBag.EmployeeName = fullName;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = hasPrevious;
            ViewBag.HasNext = hasNext;
            ViewBag.ShowReportButton = timesheetEntries.Any();

            return View(timesheetEntries);
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
        GenerateEmployeeWeeklyReport(DateTime startDate, DateTime endDate, string employeeId)
        {
            try
            {
                var timeEntries = await _context.TimeTracker
                .Where(t => t.AzureAdUserId == employeeId &&
                t.DateOfEntry >= startDate &&
                t.DateOfEntry <= endDate)
                .OrderBy(t => t.DateOfEntry)
                .ToListAsync();

                var employee = await _context.TimeTracker
                .Where(t => t.AzureAdUserId == employeeId)
                .Select(t => new { t.EmployeeName, t.EmployeeSurname, t.JobTitle, t.SupervisorFullName })
                .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return NotFound();
                }

                var totalHours = timeEntries.Sum(t => t.TotalHrsWorked);

         
                string hostCompany = string.Join(", ",
                timeEntries
                .Where(t => !string.IsNullOrWhiteSpace(t.HostCompanyName))
                .SelectMany(t => t.HostCompanyName.Split(','))
                .Select(name => Regex.Replace(name, @"\s+", " ").Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                );


                using (MemoryStream ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 25, 25, 110, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);

                    var headerPath = "assets/images/Banner.png";
                    var footerPath = "assets/images/Footer.png";
                    HeaderFooter eventHandler = new HeaderFooter(_hostingEnvironment, headerPath, footerPath);
                    writer.PageEvent = eventHandler;

                    document.Open();

                    PdfPTable employeeTable = new PdfPTable(4);
                    employeeTable.WidthPercentage = 100;
                    employeeTable.SetWidths(new float[] { 20, 30, 20, 30 });

                    AddRedLabelCell(employeeTable, "Employee Name");
                    AddWhiteValueCell(employeeTable, $"{employee.EmployeeName} {employee.EmployeeSurname}");
                    AddRedLabelCell(employeeTable, "ID Number");
                    AddWhiteValueCell(employeeTable, "");
                    AddRedLabelCell(employeeTable, "Job Title");
                    AddWhiteValueCell(employeeTable, employee.JobTitle ?? "N/A");
                    AddRedLabelCell(employeeTable, "Employment Type");
                    AddWhiteValueCell(employeeTable, "");
                    AddRedLabelCell(employeeTable, "Supervisor");
                    AddWhiteValueCell(employeeTable, employee.SupervisorFullName ?? "N/A");
                    AddRedLabelCell(employeeTable, "Host Company");
                    AddWhiteValueCell(employeeTable, hostCompany);

                    document.Add(employeeTable);
                    document.Add(new Paragraph(" ") { SpacingAfter = 10 });

                    string monthName = startDate.ToString("MMMM yyyy");
                    PdfPTable monthTable = new PdfPTable(2);
                    monthTable.WidthPercentage = 70;
                    monthTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    monthTable.SetWidths(new float[] { 60, 40 });

                    PdfPCell monthLabelCell = new PdfPCell(new Phrase("Weekly Time Sheet For the Month Of",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)));
                    monthLabelCell.BackgroundColor = new BaseColor(255, 0, 0);
                    monthLabelCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    monthLabelCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    monthLabelCell.Padding = 6;
                    monthLabelCell.BorderWidthRight = 0;
                    monthTable.AddCell(monthLabelCell);

                    PdfPCell monthValueCell = new PdfPCell(new Phrase(monthName,
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize)));
                    monthValueCell.BackgroundColor = BaseColor.WHITE;
                    monthValueCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    monthValueCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    monthValueCell.Padding = 6;
                    monthValueCell.BorderWidthLeft = 0;
                    monthTable.AddCell(monthValueCell);

                    document.Add(monthTable);
                    document.Add(new Paragraph(" ") { SpacingAfter = 10 });

                    PdfPTable timesheetTable = new PdfPTable(7);
                    timesheetTable.WidthPercentage = 100;
                    timesheetTable.SetWidths(new float[] { 8, 12, 25, 12, 12, 12, 18 });

                    AddRedHeaderCell(timesheetTable, "No");
                    AddRedHeaderCell(timesheetTable, "Date");
                    AddRedHeaderCell(timesheetTable, "Daily Task");
                    AddRedHeaderCell(timesheetTable, "Start Time");
                    AddRedHeaderCell(timesheetTable, "End Time");
                    AddRedHeaderCell(timesheetTable, "Total Hours");
                    AddRedHeaderCell(timesheetTable, "Signature");

                    int entryNumber = 1;
                    foreach (var entry in timeEntries)
                    {
                        AddTimesheetDataCell(timesheetTable, entryNumber.ToString());
                        AddTimesheetDataCell(timesheetTable, entry.DateOfEntry.ToString("yyyy-MM-dd"));
                        string cleanTask = entry.DailyTask == null ? "" : System.Text.RegularExpressions.Regex.Replace(entry.DailyTask, @"\s+", " ").Trim();
                        string truncatedTask = cleanTask.Length > 35
                            ? cleanTask.Substring(0, 35) + "..."
                            : cleanTask;

                        PdfPCell taskCell = new PdfPCell(new Phrase(truncatedTask,
                            FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)))
                        {
                            HorizontalAlignment = Element.ALIGN_LEFT,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 15
                        };

                        
                        if (cleanTask.Length > 35)
                        {
                          
                            var segments = cleanTask.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                         
                            var formattedLines = segments
                                .Select((segment, index) =>
                                    $"Host Company {index + 1}: {segment.Trim()}\n");

                           
                            string formattedTask = string.Join("\n", formattedLines);

                            var annotation = PdfAnnotation.CreateText(
                                writer,
                                null,
                                "All Tasks:\n",
                                formattedTask,
                                false,
                                "Comment"
                            );

                          
                            annotation.Put(PdfName.C, new PdfArray(new float[] { 1f, 0.5f, 0f })); // Orange

                            annotation.Put(PdfName.OPEN, new PdfBoolean(false));
                            taskCell.CellEvent = new AnnotatedCellEvent(writer, annotation);
                        }

                        timesheetTable.AddCell(taskCell);

                        if (entry.DailyTask?.Trim().Equals("Public Holiday", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            AddTimesheetDataCell(timesheetTable, ""); 
                            AddTimesheetDataCell(timesheetTable, "");
                            AddTimesheetDataCell(timesheetTable, "");
                        }
                        else
                        {
                            AddTimesheetDataCell(timesheetTable, entry.StartTime.ToString("HH:mm"));
                            AddTimesheetDataCell(timesheetTable, entry.EndTime.ToString("HH:mm"));
                            AddTimesheetDataCell(timesheetTable, FormatHours(entry.TotalHrsWorked));

                        }

                        AddSignatureDataCell(timesheetTable, "");
                        entryNumber++;
                    }

                    document.Add(timesheetTable);

                    PdfPTable totalHoursTable = new PdfPTable(2);
                    totalHoursTable.WidthPercentage = 70;
                    totalHoursTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    totalHoursTable.SetWidths(new float[] { 50, 50 });

                    PdfPCell totalLabelCell = new PdfPCell(new Phrase("Total Working Hours",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)));
                    totalLabelCell.BackgroundColor = new BaseColor(255, 0, 0);
                    totalLabelCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    totalLabelCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    totalLabelCell.Padding = 8;
                    totalLabelCell.BorderWidthRight = 0;
                    totalHoursTable.AddCell(totalLabelCell);

                    PdfPCell totalValueCell = new PdfPCell(new Phrase(FormatHours(totalHours),
    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize)));
                    totalValueCell.BackgroundColor = BaseColor.WHITE;
                    totalValueCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    totalValueCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    totalValueCell.Padding = 6;
                    totalValueCell.BorderWidthLeft = 0;
                    totalHoursTable.AddCell(totalValueCell);

                    document.Add(new Paragraph(" ") { SpacingBefore = 8 });
                    document.Add(totalHoursTable);
                    document.Add(new Paragraph(" ") { SpacingBefore = 15 });

                    PdfPTable signatureTable = new PdfPTable(4);
                    signatureTable.WidthPercentage = 100;
                    signatureTable.SetWidths(new float[] { 20, 30, 20, 30 });

                    AddRedLabelCell(signatureTable, "Employee Signature");
                    AddWhiteSignatureCell(signatureTable, "");
                    AddRedLabelCell(signatureTable, "Supervisor Signature");
                    AddWhiteSignatureCell(signatureTable, "");

                    AddRedLabelCell(signatureTable, "Date");
                    AddWhiteSignatureCell(signatureTable, endDate.ToString("yyyy-MM-dd"));
                    AddRedLabelCell(signatureTable, "Date");
                    AddWhiteSignatureCell(signatureTable, endDate.ToString("yyyy-MM-dd"));

                    document.Add(signatureTable);

                    PdfPTable commentsTable = new PdfPTable(2);
                    commentsTable.WidthPercentage = 100;
                    commentsTable.SetWidths(new float[] { 20, 80 });

                    AddRedLabelCell(commentsTable, "Comments");
                    AddWhiteCommentsCell(commentsTable, "");

                    document.Add(commentsTable);
                    document.Add(new Paragraph(" ") { SpacingBefore = 10 });

                    document.Close();


                    return File(ms.ToArray(), "application/pdf",
                        $"{employee.EmployeeName}_{employee.EmployeeSurname}_Timesheet_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf");
                }
            }
            catch (Exception ex)
            {
              
                return StatusCode(500, "An error occurred while generating the report.");
            }
        }

       
        private const float BaseFontSize = 10f;  
        private const float HeaderFontSize = 11f; 
        private const float SignatureFontSize = 10f;  

       
        private void AddSignatureDataCell(PdfPTable table, string text)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text,
            FontFactory.GetFont(FontFactory.HELVETICA, SignatureFontSize)));
            cell.BackgroundColor = BaseColor.WHITE;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 10; 
            cell.MinimumHeight = 30;  
            table.AddCell(cell);
        }

      
        private void AddRedLabelCell(PdfPTable table, string text)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text,
            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)));
            cell.BackgroundColor = new BaseColor(255, 0, 0);
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private void AddWhiteValueCell(PdfPTable table, string text)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text,
            FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)));
            cell.BackgroundColor = BaseColor.WHITE;
            cell.HorizontalAlignment = Element.ALIGN_LEFT;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 5;
            cell.BorderWidthLeft = 0;
            table.AddCell(cell);
        }

        private void AddRedHeaderCell(PdfPTable table, string text)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text,
            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, HeaderFontSize, Font.NORMAL, BaseColor.WHITE)));
            cell.BackgroundColor = new BaseColor(255, 0, 0);
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private void AddTimesheetDataCell(PdfPTable table, string text)
        {
            table.AddCell(new PdfPCell(new Phrase(text,
            FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)))
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 5
            });
        }

        private void AddWhiteSignatureCell(PdfPTable table, string text)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text,
            FontFactory.GetFont(FontFactory.HELVETICA, SignatureFontSize)));
            cell.BackgroundColor = BaseColor.WHITE;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 10;  
            cell.MinimumHeight = 30;  
            cell.BorderWidthLeft = 0;
            table.AddCell(cell);
        }

        private void AddWhiteCommentsCell(PdfPTable table, string text)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text,
            FontFactory.GetFont(FontFactory.HELVETICA, BaseFontSize)));
            cell.BackgroundColor = BaseColor.WHITE;
            cell.HorizontalAlignment = Element.ALIGN_LEFT;
            cell.VerticalAlignment = Element.ALIGN_TOP;
            cell.Padding = 8;  
            cell.MinimumHeight = 50;  
            cell.BorderWidthLeft = 0;
            table.AddCell(cell);
        }

   
        private string FormatHours(double totalHours)
        {
            int hours = (int)totalHours;
            int minutes = (int)Math.Round((totalHours - hours) * 60);
            return $"{hours} Hrs {minutes:D2} Mins";
        }

    }
}

