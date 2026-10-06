using iTextSharp.text;
using iTextSharp.text.pdf;
using PSS_Time_Tracker.Models;
using TimeSheetRecorder;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Renders the "Employee Leave Application" PDF as a field-for-field replica of the real Providence
    /// Managed Services paper form (docs - "Leave Form Template-Final March 2025 1.pdf"), including its
    /// exact banner/footer graphics (extracted losslessly from that PDF - see
    /// wwwroot/assets/images/LeaveFormHeader.png and LeaveFormFooter.png) and its exact field wording,
    /// since this is an official HR document meant to be printed and filed alongside the paper original.
    /// Checkboxes are rendered as "[X]"/"[ ]" rather than Unicode ballot-box glyphs - iTextSharp 5.5's
    /// base14 Helvetica font has no glyph for those characters, and this stays reliably renderable.
    /// </summary>
    public class LeaveRequestPdfService : ILeaveRequestPdfService
    {
        private const float LabelFontSize = 9f;
        private const float BoldHeaderFontSize = 10f;

        private readonly IWebHostEnvironment _hostingEnvironment;

        public LeaveRequestPdfService(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public byte[] GenerateLeaveForm(LeaveRequestPdfRequest request)
        {
            string fontPath = Path.Combine(_hostingEnvironment.WebRootPath, "Whisper-Regular.ttf");
            Font signatureFont;
            if (System.IO.File.Exists(fontPath))
            {
                var signatureBaseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                signatureFont = new Font(signatureBaseFont, 14f);
            }
            else
            {
                signatureFont = FontFactory.GetFont(FontFactory.HELVETICA, LabelFontSize, Font.ITALIC);
            }

            using var ms = new MemoryStream();
            var document = new Document(PageSize.A4, 25, 25, 110, 70);
            var writer = PdfWriter.GetInstance(document, ms);

            writer.PageEvent = new HeaderFooter(_hostingEnvironment,
                "assets/images/LeaveFormHeader.png", "assets/images/LeaveFormFooter.png");

            document.Open();

            document.Add(new Paragraph("EMPLOYEE LEAVE APPLICATION",
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14f))
            { Alignment = Element.ALIGN_CENTER, SpacingAfter = 10 });

            // --- First Name / Last Name / Department, ID Number / Employee Number (+ blank box),
            //     Address During Leave / Telephone Number, Start/End/Total Days, Other/Approved Paid Leave ---
            var topTable = new PdfPTable(6) { WidthPercentage = 100 };
            topTable.SetWidths(new float[] { 12, 21, 12, 21, 12, 22 });

            AddLabel(topTable, "First Name");
            AddValue(topTable, request.FirstName);
            AddLabel(topTable, "Last Name");
            AddValue(topTable, request.LastName);
            AddLabel(topTable, "Department");
            AddValue(topTable, request.Department);

            AddLabel(topTable, "ID Number");
            AddValue(topTable, request.IdNumber);
            AddLabel(topTable, "Employee Number");
            AddValue(topTable, request.EmployeeNumber);
            var blankBox = new PdfPCell(new Phrase("")) { Colspan = 2, Rowspan = 3, MinimumHeight = 50 };
            topTable.AddCell(blankBox);

            AddValue(topTable, $"Address During Leave: {request.AddressDuringLeave}", colspan: 4);

            AddValue(topTable, $"Telephone Number: {request.TelephoneNumber}", colspan: 4);

            AddLabel(topTable, "Start Date");
            AddValue(topTable, request.StartDate.ToString("yyyy-MM-dd"));
            AddLabel(topTable, "End Date");
            AddValue(topTable, request.EndDate.ToString("yyyy-MM-dd"));
            AddLabel(topTable, "Total Days");
            AddValue(topTable, request.TotalDays.ToString("0.#"));

            bool isOtherLeave = !string.IsNullOrWhiteSpace(request.OtherLeaveDescription);
            AddCheckbox(topTable,
                "Other Leave (Please Specify)" + (isOtherLeave ? $": {request.OtherLeaveDescription}" : ""),
                isOtherLeave, colspan: 4);
            AddCheckbox(topTable, "Approved Paid Leave", !isOtherLeave, colspan: 2);

            document.Add(topTable);

            document.Add(new Paragraph(
                "I hereby affirm that the information provided in this document is accurate and true to the best of my knowledge. " +
                "Any falsie in this regard may for ground for disciplinary action.",
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, LabelFontSize))
            { SpacingBefore = 8, SpacingAfter = 8 });

            // --- Signature of Requestor / Date ---
            var requestorTable = new PdfPTable(4) { WidthPercentage = 100 };
            requestorTable.SetWidths(new float[] { 20, 50, 10, 20 });
            AddLabel(requestorTable, "Signature of Requestor");
            AddSignature(requestorTable, request.EmployeeSignature, signatureFont);
            AddLabel(requestorTable, "Date");
            AddValue(requestorTable, request.EmployeeSignatureDate?.ToString("yyyy-MM-dd") ?? "");
            document.Add(requestorTable);

            document.Add(new Paragraph("*Note: To be completed by Reporting Manager",
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, LabelFontSize, Font.NORMAL, new BaseColor(200, 0, 0)))
            { SpacingBefore = 10, SpacingAfter = 4 });

            // --- Recommendation by Reporting Manager ---
            var managerTable = new PdfPTable(4) { WidthPercentage = 100 };
            managerTable.SetWidths(new float[] { 28, 24, 24, 24 });
            AddLabel(managerTable, "Recommendation by Reporting Manager");
            AddCheckbox(managerTable, "Recommended", request.ManagerRecommendation == ManagerRecommendation.Recommended);
            AddCheckbox(managerTable, "Not Recommended", request.ManagerRecommendation == ManagerRecommendation.NotRecommended);
            AddCheckbox(managerTable, "Reschedule Request", request.ManagerRecommendation == ManagerRecommendation.RescheduleRequested);

            AddLabel(managerTable, "Leave Type:");
            AddValue(managerTable, request.LeaveTypeName, colspan: 1);
            AddLabel(managerTable, "Approved Paid Leave:");
            AddValue(managerTable,
                (request.ManagerApprovedPaidLeave == true ? "[X]" : "[ ]") + " YES   " +
                (request.ManagerApprovedPaidLeave == false ? "[X]" : "[ ]") + " NO");

            document.Add(managerTable);

            var managerRemarksTable = new PdfPTable(2) { WidthPercentage = 100 };
            managerRemarksTable.SetWidths(new float[] { 20, 80 });
            AddLabel(managerRemarksTable, "Remarks / Comments");
            AddComments(managerRemarksTable, request.ManagerRemarks ?? "");
            document.Add(managerRemarksTable);

            var managerSigTable = new PdfPTable(4) { WidthPercentage = 100 };
            managerSigTable.SetWidths(new float[] { 28, 42, 10, 20 });
            AddLabel(managerSigTable, "Signature of Reporting Manager / Designee");
            AddSignature(managerSigTable, request.ManagerSignature, signatureFont);
            AddLabel(managerSigTable, "Date");
            AddValue(managerSigTable, request.ManagerDecisionDate?.ToString("yyyy-MM-dd") ?? "");
            document.Add(managerSigTable);

            document.Add(new Paragraph(" ") { SpacingAfter = 6 });

            // --- Approved by HR Manager ---
            var hrTable = new PdfPTable(4) { WidthPercentage = 100 };
            hrTable.SetWidths(new float[] { 22, 22, 22, 34 });
            AddLabel(hrTable, "Approved by HR Manager");
            AddCheckbox(hrTable, "Approved with Pay", request.HrDecision == HrDecisionType.ApprovedWithPay);
            AddCheckbox(hrTable, "Approved without Pay", request.HrDecision == HrDecisionType.ApprovedWithoutPay);
            AddCheckbox(hrTable, "Not Approved", request.HrDecision == HrDecisionType.NotApproved);
            document.Add(hrTable);

            var hrRemarksTable = new PdfPTable(2) { WidthPercentage = 100 };
            hrRemarksTable.SetWidths(new float[] { 20, 80 });
            AddLabel(hrRemarksTable, "Remarks / Comments");
            AddComments(hrRemarksTable, request.HrRemarks ?? "");
            document.Add(hrRemarksTable);

            var hrSigTable = new PdfPTable(4) { WidthPercentage = 100 };
            hrSigTable.SetWidths(new float[] { 28, 42, 10, 20 });
            AddLabel(hrSigTable, "Signature of HR Manager / Designee");
            AddSignature(hrSigTable, request.HrSignature, signatureFont);
            AddLabel(hrSigTable, "Date");
            AddValue(hrSigTable, request.HrDecisionDate?.ToString("yyyy-MM-dd") ?? "");
            document.Add(hrSigTable);

            document.Add(new Paragraph(" ") { SpacingAfter = 6 });

            // --- Captured By / Verified By / Area-Branch-Division / Country ---
            var adminTable = new PdfPTable(4) { WidthPercentage = 100 };
            adminTable.SetWidths(new float[] { 25, 25, 25, 25 });
            AddLabel(adminTable, "Captured By");
            AddValue(adminTable, request.CapturedByName ?? "");
            AddLabel(adminTable, "Captured date");
            AddValue(adminTable, request.CapturedDate?.ToString("yyyy-MM-dd") ?? "");
            AddLabel(adminTable, "Verified By");
            AddValue(adminTable, request.VerifiedByName ?? "");
            AddLabel(adminTable, "Verified date");
            AddValue(adminTable, request.VerifiedDate?.ToString("yyyy-MM-dd") ?? "");
            AddLabel(adminTable, "Area | Branch | Division");
            AddValue(adminTable, request.AreaBranchDivision);
            AddLabel(adminTable, "Country");
            AddValue(adminTable, request.Country);
            document.Add(adminTable);

            // --- Administration / Payroll ---
            var payrollHeaderTable = new PdfPTable(1) { WidthPercentage = 100 };
            var payrollHeaderCell = new PdfPCell(new Phrase("Administration / Payroll",
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, BoldHeaderFontSize)))
            {
                BackgroundColor = new BaseColor(220, 220, 220),
                Padding = 5
            };
            payrollHeaderTable.AddCell(payrollHeaderCell);
            document.Add(payrollHeaderTable);

            var payrollTable = new PdfPTable(6) { WidthPercentage = 100 };
            payrollTable.SetWidths(new float[] { 18, 15, 18, 15, 18, 16 });
            AddLabel(payrollTable, "Leave Available");
            AddValue(payrollTable, request.LeaveAvailableDays?.ToString("0.#") ?? "");
            AddLabel(payrollTable, "Leave Granted");
            AddValue(payrollTable, request.LeaveGrantedDays?.ToString("0.#") ?? "");
            AddLabel(payrollTable, "Balance");
            AddValue(payrollTable, request.LeaveBalanceDays?.ToString("0.#") ?? "");
            document.Add(payrollTable);

            document.Close();

            return ms.ToArray();
        }

        private static void AddLabel(PdfPTable table, string text, int colspan = 1, int rowspan = 1)
        {
            var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, LabelFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 4,
                Colspan = colspan,
                Rowspan = rowspan
            };
            table.AddCell(cell);
        }

        private static void AddValue(PdfPTable table, string text, int colspan = 1, int rowspan = 1)
        {
            var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, LabelFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 4,
                Colspan = colspan,
                Rowspan = rowspan,
                MinimumHeight = 18
            };
            table.AddCell(cell);
        }

        private static void AddCheckbox(PdfPTable table, string label, bool isChecked, int colspan = 1)
        {
            var text = (isChecked ? "[X] " : "[ ] ") + label;
            var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, LabelFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                Padding = 4,
                Colspan = colspan
            };
            table.AddCell(cell);
        }

        private static void AddSignature(PdfPTable table, string? text, Font signatureFont)
        {
            table.AddCell(SignaturePdfHelper.BuildCell(
                text, signatureFont, 170f, 34f, padding: 4, minHeight: 36, horizontalAlignment: Element.ALIGN_LEFT));
        }

        private static void AddComments(PdfPTable table, string text)
        {
            var cell = new PdfPCell(new Phrase(text, FontFactory.GetFont(FontFactory.HELVETICA, LabelFontSize)))
            {
                BackgroundColor = BaseColor.WHITE,
                HorizontalAlignment = Element.ALIGN_LEFT,
                VerticalAlignment = Element.ALIGN_TOP,
                Padding = 6,
                MinimumHeight = 45
            };
            table.AddCell(cell);
        }
    }
}
