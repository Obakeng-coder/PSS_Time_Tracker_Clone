-- =====================================================================
-- PSS TymSheet (local) -- combined stored procedures
-- Run this once against your Timesheet database AFTER `dotnet ef database
-- update` has created the tables. Safe to re-run (each proc is dropped
-- first if it already exists).
-- =====================================================================

-- ---------------------------------------------------------------------
-- Source: Track Your Time_Pull User Data Procedure.sql
-- ---------------------------------------------------------------------
-- ================================================
-- In "Track Your Time" feature this Procedure simulates pulling the data
-- Index for TimeTrackerController
-- ================================================
CREATE OR ALTER PROCEDURE [dbo].[GetUserTimeEntriesCount]
    @AzureAdUserId NVARCHAR(450),
    @StartDate DATETIME = NULL,
    @EndDate DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS TotalCount
    FROM TimeTracker
    WHERE 
        AzureAdUserId = @AzureAdUserId
        AND (@StartDate IS NULL OR DateOfEntry >= @StartDate)
        AND (@EndDate IS NULL OR DateOfEntry <= @EndDate);
END
GO

-- ---------------------------------------------------------------------
-- Source: Track Your Time_Pagination Procedure.sql
-- ---------------------------------------------------------------------
-- ================================================
-- GetUserTimeEntriesPaged 
-- This Procedure is created for Track Your Time" 
-- feature to work for the Pagination feature
-- Index for TimeTrackerController
-- 
-- ================================================
CREATE OR ALTER PROCEDURE [dbo].[GetUserTimeEntriesPaged]
    @AzureAdUserId NVARCHAR(450),
    @StartDate DATETIME = NULL,
    @EndDate DATETIME = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT 
        TimeTrackerId,   
        AzureAdUserId,
        EmployeeName,
        EmployeeSurname,
        JobTitle,
        SupervisorFullName,
        HostCompanyName,
        TimeSheetMonth,
        DateOfEntry,
        StartTime,
        EndTime,
        TotalHrsWorked,
        DailyTask,
        UserAccountAzureAdUserId,
        IsPublicHoliday
    FROM 
        TimeTracker
    WHERE 
        AzureAdUserId = @AzureAdUserId
        AND (@StartDate IS NULL OR DateOfEntry >= @StartDate)
        AND (@EndDate IS NULL OR DateOfEntry <= @EndDate)
    ORDER BY 
        DateOfEntry DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ---------------------------------------------------------------------
-- Source: Manager Board_Employee Timesheets_ Counts Total Employees.sql
-- ---------------------------------------------------------------------
-- ================================================
-- GetEmployeeSummaryCount: to count total matching results for pagination
-- Manager Board/ Employee Timesheets
-- Displays list of all employees and pulls employee details
-- This page is where employees click employee name hyperlink
-- ================================================

CREATE OR ALTER PROCEDURE GetEmployeeSummaryCount
    @SearchTerm NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    WITH EmployeeCTE AS (
        SELECT 
            AzureAdUserId,
            EmployeeName,
            EmployeeSurname
        FROM TimeTracker
        GROUP BY AzureAdUserId, EmployeeName, EmployeeSurname
    )
    SELECT COUNT(*) AS TotalCount
    FROM EmployeeCTE
    WHERE 
        @SearchTerm IS NULL OR
        (EmployeeName + ' ' + EmployeeSurname LIKE '%' + @SearchTerm + '%'
         OR EmployeeName LIKE '%' + @SearchTerm + '%'
         OR EmployeeSurname LIKE '%' + @SearchTerm + '%');
END
GO

-- ---------------------------------------------------------------------
-- Source: Manager Board_Employee Timesheets_ Pagination Procedure (Display List Of Employees).sql
-- ---------------------------------------------------------------------
-- ================================================
-- GetEmployeeSummaryPaged: Manager Board/Employee Timesheets
-- For paginated employee summaries
-- This Procedure Paginates Employees
-- ================================================

CREATE OR ALTER PROCEDURE GetEmployeeSummaryPaged
    @SearchTerm NVARCHAR(100),
    @PageNumber INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    WITH EmployeeCTE AS (
        SELECT 
            AzureAdUserId,
            EmployeeName,
            EmployeeSurname,
            COUNT(*) AS TimesheetCount,
            MAX(DateOfEntry) AS LatestEntry
        FROM TimeTracker
        GROUP BY AzureAdUserId, EmployeeName, EmployeeSurname
    ),
    Filtered AS (
        SELECT *
        FROM EmployeeCTE
        WHERE 
            @SearchTerm IS NULL OR
            (EmployeeName + ' ' + EmployeeSurname LIKE '%' + @SearchTerm + '%'
             OR EmployeeName LIKE '%' + @SearchTerm + '%'
             OR EmployeeSurname LIKE '%' + @SearchTerm + '%')
    )
    SELECT *
    FROM Filtered
    ORDER BY EmployeeName, EmployeeSurname
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ---------------------------------------------------------------------
-- Source: Manager Board_Employee Timesheets_Personalised Employee (Deals with Pulling Employee data).sql
-- ---------------------------------------------------------------------
-- ==-- ================================================
-- Manager Board/ Employee Timesheets/ Persnoalised Employee
-- This Procedure deals with getting count of personalised employee data

-- This procedure shows only employees related to mager
-- ================================================
CREATE OR ALTER PROCEDURE GetManagerEmployeeTimeSheetsCount
    @SupervisorEmail NVARCHAR(256),
    @StartDate DATE = NULL,
    @EndDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS TotalCount
    FROM TimeTracker t
    INNER JOIN Users u ON t.AzureAdUserId = u.AzureAdUserId
    WHERE u.SupervisorEmail = @SupervisorEmail
        AND (@StartDate IS NULL OR t.DateOfEntry >= @StartDate)
        AND (@EndDate IS NULL OR t.DateOfEntry <= @EndDate);
END
GO

-- ---------------------------------------------------------------------
-- Source: Manager Board_Employee Timesheets_Personalised Employee (Deals with Pagination).sql
-- ---------------------------------------------------------------------
-- ================================================
-- This Procedure is for Paging and using Pagination for "Employee Timesheet"
-- This page works when u click the hyperlink and it navigates to Personalised Employee
-- This Procedure deals with Pagination 

--This procedure shows only employees related to mager
-- ================================================
CREATE OR ALTER PROCEDURE GetManagerEmployeeTimeSheetsPaged
    @SupervisorEmail NVARCHAR(256),
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @PageNumber INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH EmployeeSummary AS (
        SELECT 
            t.AzureAdUserId,
            t.EmployeeName,
            t.EmployeeSurname,
            COUNT(*) AS TimesheetCount,
            MAX(t.DateOfEntry) AS LatestEntry
        FROM TimeTracker t
        INNER JOIN Users u ON t.AzureAdUserId = u.AzureAdUserId
        WHERE u.SupervisorEmail = @SupervisorEmail
            AND (@StartDate IS NULL OR t.DateOfEntry >= @StartDate)
            AND (@EndDate IS NULL OR t.DateOfEntry <= @EndDate)
        GROUP BY t.AzureAdUserId, t.EmployeeName, t.EmployeeSurname
    )
    SELECT *
    FROM (
        SELECT 
            *,
            ROW_NUMBER() OVER (ORDER BY LatestEntry DESC) AS RowNum
        FROM EmployeeSummary
    ) AS Paged
    WHERE RowNum BETWEEN ((@PageNumber - 1) * @PageSize + 1) AND (@PageNumber * @PageSize)
    ORDER BY LatestEntry DESC;
END
GO

-- ---------------------------------------------------------------------
-- Source: GetEmployeeTimesheetCount (Manager board for specific employee).txt
-- ---------------------------------------------------------------------
/*
       This procedure returns the total count of timesheet entries
       for a specified employee within an optional date range.
       
       Parameters:
         @EmployeeId - The unique identifier (Azure AD User ID) of the employee.
         @StartDate  - Optional start date filter.
         @EndDate    - Optional end date filter.

       Functionality:
         - Counts how many entries exist in the TimeTracker table for the employee and date range.
         - This count supports pagination by allowing calculation of total pages.
*/

CREATE OR ALTER PROCEDURE GetEmployeeTimesheetCount
    @EmployeeId NVARCHAR(100),          -- The unique Azure AD user ID for the employee
    @StartDate DATE = NULL,             -- Optional filter: only include entries on or after this date
    @EndDate DATE = NULL                -- Optional filter: only include entries on or before this date
AS
BEGIN
    SELECT COUNT(*)                     -- Return the total number of entries that match the criteria
    FROM TimeTracker
    WHERE AzureAdUserId = @EmployeeId                                -- Filter to only include records for the specified employee
      AND (@StartDate IS NULL OR DateOfEntry >= @StartDate)          -- Apply start date filter if provided
      AND (@EndDate IS NULL OR DateOfEntry <= @EndDate)              -- Apply end date filter if provided
END
GO

-- ---------------------------------------------------------------------
-- Source: GetEmployeeTimesheetPaged (Manager board for specific employee).txt
-- ---------------------------------------------------------------------
/*
       ----------------------------------------
       This procedure retrieves a paginated list of timesheet entries
       for a specified employee within an optional date range.
       
       Parameters:
         @EmployeeId - The unique identifier (Azure AD User ID) of the employee.
         @StartDate  - Optional start date filter to include entries on or after this date.
         @EndDate    - Optional end date filter to include entries on or before this date.
         @PageNumber - The current page number (for pagination).
         @PageSize   - The number of records to return per page.

       Functionality:
         - Filters the TimeTracker table for the given employee and date range.
         - Uses ROW_NUMBER() to assign row numbers to entries ordered by DateOfEntry descending.
         - Returns only the rows that belong to the requested page based on PageNumber and PageSize.
         - Ensures efficient pagination by limiting results directly at the database level.
         - Orders results by the most recent DateOfEntry first.
*/

CREATE OR ALTER PROCEDURE GetEmployeeTimesheetPaged
    @EmployeeId NVARCHAR(100),          -- The unique Azure AD user ID for the employee
    @StartDate DATE = NULL,             -- Optional filter: only include entries on or after this date
    @EndDate DATE = NULL,               -- Optional filter: only include entries on or before this date
    @PageNumber INT,                    -- Current page number for pagination
    @PageSize INT                       -- Number of records to return per page
AS
BEGIN
    SET NOCOUNT ON;                     -- Prevents extra result sets from interfering with the output (improves performance and avoids issues in some apps)

    SELECT *
    FROM (
        SELECT *, 
               ROW_NUMBER() OVER (ORDER BY DateOfEntry DESC) AS RowNum  -- Assigns a unique row number to each row, ordered by most recent DateOfEntry first
        FROM TimeTracker
        WHERE AzureAdUserId = @EmployeeId                                -- Filter to only include records for the specified employee
          AND (@StartDate IS NULL OR DateOfEntry >= @StartDate)         -- Apply start date filter if provided
          AND (@EndDate IS NULL OR DateOfEntry <= @EndDate)             -- Apply end date filter if provided
    ) AS Filtered
    WHERE RowNum BETWEEN (@PageNumber - 1) * @PageSize + 1              -- Skip rows before the current page
                    AND @PageNumber * @PageSize                         -- Include only the number of rows for this page
    ORDER BY DateOfEntry DESC;                                          -- Final ordering of results (most recent first)
END
GO

-- ---------------------------------------------------------------------
-- Source: Manager Board_Approve Employees (Deals with Pulling Approved Users).sql
-- ---------------------------------------------------------------------
-- ================================================
-- Manager Board/Approve Employees
-- Deals With pulled employees and pulls them via Procedure tester
-- ================================================
CREATE OR ALTER PROCEDURE GetUsersNeedingApprovalCount
    @SearchTerm NVARCHAR(100) = NULL,
    @SupervisorFullName NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS TotalCount
    FROM Users
    WHERE ApprovalStatus = 0
      AND (@SupervisorFullName IS NULL OR SupervisorFullName = @SupervisorFullName)
      AND (
            @SearchTerm IS NULL OR @SearchTerm = '' OR
            (EmployeeName + ' ' + EmployeeSurname) LIKE '%' + @SearchTerm + '%' OR
            EmployeeName LIKE '%' + @SearchTerm + '%' OR
            EmployeeSurname LIKE '%' + @SearchTerm + '%'
        );
END
GO

-- ---------------------------------------------------------------------
-- Source: Manager Board_Approve Employees (Deals with Pagination).sql
-- ---------------------------------------------------------------------
-- ================================================
-- Manager Board/ Requires admin approval
-- Deal with Pagination for Paged employees requiring approval
-- sdsad
-- ================================================
CREATE OR ALTER PROCEDURE GetUsersNeedingApprovalPaged
    @SearchTerm NVARCHAR(100) = NULL,
    @SupervisorFullName NVARCHAR(200) = NULL,
    @PageNumber INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM (
        SELECT 
            *, 
            ROW_NUMBER() OVER (ORDER BY EmployeeSurname, EmployeeName) AS RowNum
        FROM Users
        WHERE ApprovalStatus = 0
          AND (@SupervisorFullName IS NULL OR SupervisorFullName = @SupervisorFullName)
          AND (
                @SearchTerm IS NULL OR @SearchTerm = '' OR
                (EmployeeName + ' ' + EmployeeSurname) LIKE '%' + @SearchTerm + '%' OR
                EmployeeName LIKE '%' + @SearchTerm + '%' OR
                EmployeeSurname LIKE '%' + @SearchTerm + '%'
          )
    ) AS Paged
    WHERE RowNum BETWEEN ((@PageNumber - 1) * @PageSize + 1) AND (@PageNumber * @PageSize)
    ORDER BY RowNum;
END
GO

