using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeSheetRecorder.Models;
using TimeSheetRecorder.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using iTextSharp.text.pdf;
using iTextSharp.text;
using TimeSheetRecorder;
using System.Text.RegularExpressions;

namespace PSS_Time_Tracker.Controllers
{
    [Authorize]
    public class TimeTrackerController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly ILogger<TimeTrackerController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public TimeTrackerController(
            timeSheetRecorderContext context,
            ILogger<TimeTrackerController> logger,
            IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Originally this called Microsoft Graph to fetch the signed-in user's job title
            // and manager name. Now that data already lives on the UserAccount row that was
            // created when the person logged in via the local Login screen, so we just read it.
            var userId = User.GetUserId();
            var userAccount = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);

            var hostCompanies = await _context.HostCompanies
                .OrderBy(h => h.Name)
                .Select(h => h.Name)
                .ToListAsync();

            var model = new TimeTrackerViewModel
            {
                EmployeeName = userAccount?.EmployeeName ?? "",
                EmployeeSurname = userAccount?.EmployeeSurname ?? "",
                JobTitle = userAccount?.JobTitle ?? ".",
                SupervisorFullName = userAccount?.SupervisorFullName ?? ".",
                AzureAdUserId = userId,
                DateOfEntry = DateTime.Today,
                TimeSheetMonth = "",
                MonthOptions = new List<string>(),
                HostCompanyOptions = hostCompanies
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TimeTrackerViewModel viewModel)
        {
           
            viewModel.TimeSheetMonth = viewModel.DateOfEntry.ToString("MMMM yyyy");



           
            double totalHoursDecimal = 0;
            if (!viewModel.IsPublicHoliday)
            {
                if (!string.IsNullOrWhiteSpace(viewModel.TotalHrsWorked))
                {
                    
                    var parts = viewModel.TotalHrsWorked.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes))
                    {
                        totalHoursDecimal = Math.Round(hours + (minutes / 60.0), 2);
                    }
                    else
                    {
                        ModelState.AddModelError("TotalHrsWorked", "Invalid total hours format. Expected HH:mm.");
                    }
                }
                else
                {
                    ModelState.AddModelError("TotalHrsWorked", "Total hours worked is required.");
                }
            }

          
            if (viewModel.IsPublicHoliday)
            {
                viewModel.HostCompanyName = "PSS";
                totalHoursDecimal = 8;
                viewModel.TotalHrsWorked = "8:00"; 
                viewModel.StartTime = DateTime.Today; 
                viewModel.EndTime = DateTime.Today;   
                viewModel.DailyTask = "Public Holiday";
            }

            
            if (viewModel.DateOfEntry.DayOfWeek == System.DayOfWeek.Saturday ||
                viewModel.DateOfEntry.DayOfWeek == System.DayOfWeek.Sunday)
            {
                ModelState.AddModelError("DateOfEntry", "You can only select dates from Monday to Friday.");
            }

          
            var today = DateTime.Today;
            var dayOfWeek = today.DayOfWeek;
            var mondayOffset = dayOfWeek == System.DayOfWeek.Sunday ? -6 : (int)System.DayOfWeek.Monday - (int)dayOfWeek;
            var currentMonday = today.AddDays(mondayOffset);
            var currentFriday = currentMonday.AddDays(4);

           
            if (viewModel.DateOfEntry < currentMonday || viewModel.DateOfEntry > currentFriday)
            {
                ModelState.AddModelError("DateOfEntry",
                    $"You can only select dates from {currentMonday:yyyy-MM-dd} to {currentFriday:yyyy-MM-dd} (current week).");
            }

           
            if (!viewModel.IsPublicHoliday &&
     viewModel.StartTime.HasValue && viewModel.EndTime.HasValue &&
     viewModel.StartTime.Value.TimeOfDay == TimeSpan.Zero &&
     viewModel.EndTime.Value.TimeOfDay == TimeSpan.Zero)
            {
                ModelState.AddModelError("", "Start Time and End Time cannot both be 00:00 unless it is a Public Holiday.");
            }

            // --- Handle Host Company Name ---
            if (string.IsNullOrEmpty(viewModel.HostCompanyName))
            {
                // Always prefer SelectedHostCompanies if present
                if (viewModel.SelectedHostCompanies != null && viewModel.SelectedHostCompanies.Any())
                {
                    viewModel.HostCompanyName = string.Join(", ", viewModel.SelectedHostCompanies);
                }
                else if (!string.IsNullOrEmpty(viewModel.HostCompanyName))
                {
                    // Single host company mode
                    viewModel.HostCompanyName = viewModel.HostCompanyName.Trim();
                }
                else
                {
                    ModelState.AddModelError("HostCompanyName", "Host Company Name is required.");
                }

            }

         
            if (string.IsNullOrEmpty(viewModel.DailyTask))
            {
                if (viewModel.DailyTasksPerCompany != null && viewModel.DailyTasksPerCompany.Any())
                {
                    viewModel.DailyTask = string.Join("; ",
                        viewModel.DailyTasksPerCompany
                            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                            .Select(kvp => $"{kvp.Key}: {kvp.Value.Trim()}"));
                }
                else
                {
                    ModelState.AddModelError("DailyTask", "Daily Task is required.");
                }
            }

            if (!ModelState.IsValid)
            {
                // Collect all validation errors
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

               
                return Json(new
                {
                    showValidationError = true,
                    errorMessages = errorMessages
                });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var userId = User.GetUserId();

                    // First check for duplicate entry for this date
                    var existingEntry = await _context.TimeTracker
                        .FirstOrDefaultAsync(t => t.AzureAdUserId == userId &&
                                                t.DateOfEntry.Date == viewModel.DateOfEntry.Date);

                    if (existingEntry != null)
                    {
                        TempData["DuplicateDateError"] = $"You have already submitted a timesheet for {viewModel.DateOfEntry:yyyy-MM-dd}.";
                        return Json(new
                        {
                            showDuplicateModal = true,
                            errorMessage = TempData["DuplicateDateError"]
                        });
                    }

                    // Get user's approval status
                    var userAccount = await _context.Users
                        .FirstOrDefaultAsync(u => u.AzureAdUserId == userId);


                    // If users already applied for admin approval
                    if (userAccount?.ApprovalStatus == 0)
                    {
                        return Json(new
                        {
                            showAlreadyRequestedModal = true,
                            alreadyRequestedMessage = "You have already requested admin approval. Please wait for an admin to approve your request before submitting a new timesheet."
                        });
                    }

                    // If approval status is 3 (rejected), show modal and do not save
                    if (userAccount?.ApprovalStatus == 3)
                    {
                        return Json(new
                        {
                            showRejectedModal = true,
                            rejectedMessage = "Your initial admin approval request was rejected. You can apply again after 24 hours of the initial application."
                        });
                    }

                    // Check if user has any entries in current week
                    var hasEntriesInCurrentWeek = await _context.TimeTracker
                        .AnyAsync(t => t.AzureAdUserId == userId &&
                                      t.DateOfEntry >= currentMonday &&
                                      t.DateOfEntry <= currentFriday);

                    // Only check for missed previous week if:
                    // 1. User doesn't have approved status (2)
                    // 2. Doesn't have any entries in current week yet
                    // 3. Is trying to submit for current week
                    // Check if user has any timesheet entries at all
                    var isFirstTimeUser = !await _context.TimeTracker
                        .AnyAsync(t => t.AzureAdUserId == userId);

                    if (!isFirstTimeUser &&
                        userAccount?.ApprovalStatus != 2 &&
                        !hasEntriesInCurrentWeek &&
                        viewModel.DateOfEntry >= currentMonday)
                    {
                        var previousMonday = currentMonday.AddDays(-7);
                        var previousFriday = currentFriday.AddDays(-7);

                        var hasEntriesForPreviousWeek = await _context.TimeTracker
                            .AnyAsync(t => t.AzureAdUserId == userId &&
                                          t.DateOfEntry >= previousMonday &&
                                          t.DateOfEntry <= previousFriday);

                        if (!hasEntriesForPreviousWeek)
                        {
                            TempData["MissedWeekStart"] = previousMonday.ToString("dd/MM/yyyy");
                            TempData["MissedWeekEnd"] = previousFriday.ToString("dd/MM/yyyy");
                            return Json(new
                            {
                                showModal = true,
                                missedWeekStart = previousMonday.ToString("dd/MM/yyyy"),
                                missedWeekEnd = previousFriday.ToString("dd/MM/yyyy")
                            });
                        }
                    }

                    // Employee/manager details now come from the UserAccount row created at
                    // login time, instead of a live Microsoft Graph lookup.
                    var timeTracker = new TimeTrackerModel
                    {
                        AzureAdUserId = userId,
                        EmployeeName = userAccount?.EmployeeName ?? "",
                        EmployeeSurname = userAccount?.EmployeeSurname ?? "",
                        JobTitle = userAccount?.JobTitle ?? ".",
                        SupervisorFullName = userAccount?.SupervisorFullName ?? ".",
                        HostCompanyName = viewModel.HostCompanyName,
                        TimeSheetMonth = viewModel.TimeSheetMonth,
                        DateOfEntry = viewModel.DateOfEntry,
                        StartTime = viewModel.StartTime.Value,
                        EndTime = viewModel.EndTime.Value,
                        TotalHrsWorked = totalHoursDecimal,
                        DailyTask = viewModel.DailyTask,
                        IsPublicHoliday = viewModel.IsPublicHoliday
                    };

                    _context.TimeTracker.Add(timeTracker);

                    // If user was approved (status = 2), reset to normal status (1) after submission
                    if (userAccount?.ApprovalStatus == 2)
                    {
                        userAccount.ApprovalStatus = 1;
                        _context.Users.Update(userAccount);
                    }

                    await _context.SaveChangesAsync();

                    return Json(new { redirectUrl = Url.Action(nameof(Index)) });
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while saving timesheet");
                    ModelState.AddModelError("", "An error occurred while saving your time entry.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while saving timesheet");
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }

            if (!ModelState.IsValid)
            {
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        _logger.LogWarning("ModelState error for {Key}: {ErrorMessage}", kvp.Key, error.ErrorMessage);
                    }
                }
            }

            return View(viewModel);
        }



        [HttpPost]
        public async Task<IActionResult> RequestAdminApproval()
        {
            try
            {
                var userId = User.GetUserId();
                var userAccount = await _context.Users
                    .FirstOrDefaultAsync(u => u.AzureAdUserId == userId);

                if (userAccount != null)
                {
                    userAccount.ApprovalStatus = 0; // Requested approval
                    _context.Users.Update(userAccount);
                    await _context.SaveChangesAsync();

                    // Send email to manager
                    if (!string.IsNullOrWhiteSpace(userAccount.SupervisorEmail))
                    {
                        var emailService = HttpContext.RequestServices.GetRequiredService<EmailService>();
                        await emailService.SendAdminApprovalRequestToManagerAsync(
                            userAccount.SupervisorEmail,
                            userAccount.SupervisorFullName,
                            $"{userAccount.EmployeeName} {userAccount.EmployeeSurname}",
                            userAccount.Email
                        );
                    }

                    TempData["ApprovalMessage"] = "Your request for admin approval has been submitted.";
                }
                else
                {
                    TempData["ApprovalMessage"] = "User account not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting admin approval");
                TempData["ApprovalMessage"] = "An error occurred while submitting your request.";
            }

            return RedirectToAction("Create");
        }


        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, int page = 1)
        {
            var userId = User.GetUserId();
            int pageSize = 10; // Number of entries per page

            var timeEntries = await _context.TimeTracker
         .FromSqlInterpolated($"EXEC GetUserTimeEntriesPaged @AzureAdUserId={userId}, @StartDate={startDate}, @EndDate={endDate}, @PageNumber={page}, @PageSize={pageSize}")
         .AsNoTracking()
         .ToListAsync();

            var totalRecords = _context.SpResults
                .FromSqlInterpolated($"EXEC GetUserTimeEntriesCount @AzureAdUserId={userId}, @StartDate={startDate}, @EndDate={endDate}")
                .AsNoTracking()
                .AsEnumerable() // 👈 move execution to client side
                .Select(r => r.TotalCount)
                .FirstOrDefault();



            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            // Set ViewBag values for date filters
            if (startDate.HasValue)
            {
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            }

            if (endDate.HasValue)
            {
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }

            // Check for complete week if dates are filtered
            bool showReportButton = false;
            if (startDate.HasValue && endDate.HasValue)
            {
                // Check if the filtered range is exactly one week (Monday to Friday)
                TimeSpan span = endDate.Value - startDate.Value;
                if (span.Days == 4 && startDate.Value.DayOfWeek == System.DayOfWeek.Monday && endDate.Value.DayOfWeek == System.DayOfWeek.Friday)
                {
                    // Check if we have entries for all weekdays
                    var datesInRange = Enumerable.Range(0, 5)
                        .Select(offset => startDate.Value.AddDays(offset))
                        .ToList();

                    var entryDates = timeEntries
                        .Select(e => e.DateOfEntry.Date)
                        .Distinct()
                        .ToList();

                    showReportButton = datesInRange.All(d => entryDates.Contains(d));
                }
            }

            ViewBag.ShowReportButton = showReportButton;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < totalPages;

            return View(timeEntries);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateWeeklyReport(DateTime startDate, DateTime endDate, string signature)
        {
            try
            {
                var userId = User.GetUserId();

                // Get time entries for the selected week
                var timeEntries = await _context.TimeTracker
                    .Where(t => t.AzureAdUserId == userId &&
                                t.DateOfEntry >= startDate &&
                                t.DateOfEntry <= endDate)
                    .OrderBy(t => t.DateOfEntry)
                    .ToListAsync();

                var employee = await _context.TimeTracker
         .Where(t => t.AzureAdUserId == userId)
         .Select(t => new { t.EmployeeName, t.EmployeeSurname, t.JobTitle, t.SupervisorFullName })
         .FirstOrDefaultAsync();


                if (employee == null)
                {
                    return NotFound();
                }



                // Calculate total hours for the week
                var totalHours = timeEntries.Sum(t => t.TotalHrsWorked);

                // Flatten and deduplicate host company names
                string hostCompany = string.Join(", ",
                timeEntries
                .Where(t => !string.IsNullOrWhiteSpace(t.HostCompanyName))
                .SelectMany(t => t.HostCompanyName.Split(','))
                .Select(name => Regex.Replace(name, @"\s+", " ").Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                );




                // Load the Whisper font for signature
                string fontPath = Path.Combine(_hostingEnvironment.WebRootPath, "Whisper-Regular.ttf");


                BaseFont signatureBaseFont = null;
                Font signatureFont = null;
                if (System.IO.File.Exists(fontPath))
                {
                    signatureBaseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    signatureFont = new Font(signatureBaseFont, 15f); // 18pt for signature look
                }
                else
                {
                    // Fallback to Helvetica if font not found
                    signatureFont = FontFactory.GetFont(FontFactory.HELVETICA, 10f, Font.ITALIC);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 25, 25, 110, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);

                    // Add header and footer
                    var headerPath = "assets/images/Banner.png";
                    var footerPath = "assets/images/Footer.png";
                    HeaderFooter eventHandler = new HeaderFooter(_hostingEnvironment, headerPath, footerPath);
                    writer.PageEvent = eventHandler;

                    document.Open();

                    // Create employee info table with 4 columns
                    PdfPTable employeeTable = new PdfPTable(4);
                    employeeTable.WidthPercentage = 100;
                    employeeTable.SetWidths(new float[] { 20, 30, 20, 30 });

                    AddRedLabelCell(employeeTable, "Employee Name");
                    AddWhiteValueCell(employeeTable, $"{employee.EmployeeName} {employee.EmployeeSurname}");
                    AddRedLabelCell(employeeTable, "ID Number");
                    AddWhiteValueCell(employeeTable, "");
                    AddRedLabelCell(employeeTable, "Job Title");
                    AddWhiteValueCell(employeeTable, string.IsNullOrWhiteSpace(employee.JobTitle) ? "" : employee.JobTitle);
                    AddRedLabelCell(employeeTable, "Employment Type");
                    AddWhiteValueCell(employeeTable, "");
                    AddRedLabelCell(employeeTable, "Supervisor");
                    AddWhiteValueCell(employeeTable, employee.SupervisorFullName ?? ".");
                    AddRedLabelCell(employeeTable, "Host Company");
                    AddWhiteValueCell(employeeTable, hostCompany);


                    document.Add(employeeTable);
                    document.Add(new Paragraph(" ") { SpacingAfter = 10 });

                    // Add "Weekly Time Sheet For the Month Of" section
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

                    // Create timesheet table with 7 columns (added Signature column)
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

                        string cleanTask = Regex.Replace(entry.DailyTask ?? "", @"\s+", " ").Trim();
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

                        // Only attach annotation if task is longer than 45 characters
                        if (cleanTask.Length > 35)
                        {
                            // Split by semicolon
                            var segments = cleanTask.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                            // Add prefix and separator lines
                            var formattedLines = segments
                                .Select((segment, index) =>
                                    $"Host Company {index + 1}: {segment.Trim()}\n");

                            // Join with newlines (each task gets a divider below it)
                            string formattedTask = string.Join("\n", formattedLines);

                            var annotation = PdfAnnotation.CreateText(
                                writer,
                                null,
                                "All Tasks:\n",
                                formattedTask,
                                false,
                                "Comment"
                            );

                            // Set the annotation icon color (might be ignored by viewer)
                            annotation.Put(PdfName.C, new PdfArray(new float[] { 1f, 0.5f, 0f })); // Orange

                            annotation.Put(PdfName.OPEN, new PdfBoolean(false));
                            taskCell.CellEvent = new AnnotatedCellEvent(writer, annotation);
                        }



                        timesheetTable.AddCell(taskCell);



                        if (entry.DailyTask?.Trim().Equals("Public Holiday", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            AddTimesheetDataCell(timesheetTable, ""); // Start Time blank
                            AddTimesheetDataCell(timesheetTable, ""); // End Time blank
                            AddTimesheetDataCell(timesheetTable, ""); // Total Hours blank
                        }
                        else
                        {
                            AddTimesheetDataCell(timesheetTable, entry.StartTime.ToString("HH:mm"));
                            AddTimesheetDataCell(timesheetTable, entry.EndTime.ToString("HH:mm"));
                            AddTimesheetDataCell(timesheetTable, FormatHours(entry.TotalHrsWorked));

                        }


                        // Use the script font for the signature
                        AddSignatureDataCell(timesheetTable, signature ?? "", signatureFont);
                        entryNumber++;
                    }

                    document.Add(timesheetTable);

                    // Create total hours table
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

                    // Create signature table with 4 columns (red label | white box | red label | white box)
                    PdfPTable signatureTable = new PdfPTable(4);
                    signatureTable.WidthPercentage = 100;
                    signatureTable.SetWidths(new float[] { 20, 30, 20, 30 });

                    AddRedLabelCell(signatureTable, "Employee Signature");
                    // Use the script font and the signature value
                    AddWhiteSignatureCell(signatureTable, signature ?? "", signatureFont);
                    AddRedLabelCell(signatureTable, "Supervisor Signature");
                    AddWhiteSignatureCell(signatureTable, "");

                    AddRedLabelCell(signatureTable, "Date");
                    AddWhiteSignatureCell(signatureTable, endDate.ToString("yyyy-MM-dd"));
                    AddRedLabelCell(signatureTable, "Date");
                    AddWhiteSignatureCell(signatureTable, endDate.ToString("yyyy-MM-dd"));


                    document.Add(signatureTable);

                    // Create comments table with 2 columns (red label | white box)
                    PdfPTable commentsTable = new PdfPTable(2);
                    commentsTable.WidthPercentage = 100;
                    commentsTable.SetWidths(new float[] { 20, 80 });

                    AddRedLabelCell(commentsTable, "Comments");
                    AddWhiteCommentsCell(commentsTable, "");

                    document.Add(commentsTable);

                    Paragraph footer = new Paragraph($"",
                        FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9));
                    footer.Alignment = Element.ALIGN_RIGHT;
                    document.Add(footer);

                    document.Close();

                    return File(ms.ToArray(), "application/pdf",
      $"{employee.EmployeeName}_{employee.EmployeeSurname}_Timesheet_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf");

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating weekly report PDF");
                throw;
            }
        }


        // Add these constants at the top of your class
        private const float BaseFontSize = 10f;  // Main content font size
        private const float HeaderFontSize = 11f;  // For headers and labels
        private const float SignatureFontSize = 10f;  // For signature areas

        // Updated helper for signature cell
        private void AddSignatureDataCell(PdfPTable table, string text, Font signatureFont)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, signatureFont));
            cell.BackgroundColor = BaseColor.WHITE;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 10;
            cell.MinimumHeight = 30;
            table.AddCell(cell);
        }


        private void AddWhiteSignatureCell(PdfPTable table, string text, Font signatureFont)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, signatureFont));
            cell.BackgroundColor = BaseColor.WHITE;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 10;
            cell.MinimumHeight = 30;
            cell.BorderWidthLeft = 0;
            table.AddCell(cell);
        }


        // Helper methods for PDF generation (updated with consistent font sizes)
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
            cell.Padding = 10;  // Reduced from 15
            cell.MinimumHeight = 30;  // Reduced from 40
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
            cell.Padding = 8;  // Reduced from 10
            cell.MinimumHeight = 50;  // Reduced from 60
            cell.BorderWidthLeft = 0;
            table.AddCell(cell);
        }

        // Converts decimal hours (e.g., 7.63) to "7 Hrs 38 Mins"
        private string FormatHours(double totalHours)
        {
            int hours = (int)totalHours;
            int minutes = (int)Math.Round((totalHours - hours) * 60);
            return $"{hours} Hrs {minutes:D2} Mins";
        }

    }
}