-- ---------------------------------------------------------------------
-- Source: Manager Board_Employee Timesheets_Personalised Employee (Deals with Pulling Employee data).sql
-- ---------------------------------------------------------------------
-- ================================================
-- Manager Board/ Employee Timesheets/ Personalised Employee
-- This Procedure deals with getting count of personalised employee data
--
-- Counts employees reporting to the manager (Users, filtered by SupervisorEmail), not
-- timesheet rows - paired with GetManagerEmployeeTimeSheetsPaged.sql's LEFT JOIN fix, this
-- counts every employee whether or not they have a TimeTracker row yet. Previously
-- (INNER JOIN TimeTracker) an employee with none was excluded from both the page and the
-- total, so pagination undercounted right along with the list itself.
--
-- @SearchTerm now actually filters the count (previously accepted by neither this proc nor
-- its caller).
-- ================================================
CREATE OR ALTER PROCEDURE GetManagerEmployeeTimeSheetsCount
    @SupervisorEmail NVARCHAR(256),
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @SearchTerm NVARCHAR(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) AS TotalCount
    FROM Users u
    WHERE u.SupervisorEmail = @SupervisorEmail
        AND (
            @SearchTerm IS NULL OR @SearchTerm = '' OR
            (u.EmployeeName + ' ' + u.EmployeeSurname) LIKE '%' + @SearchTerm + '%' OR
            u.EmployeeName LIKE '%' + @SearchTerm + '%' OR
            u.EmployeeSurname LIKE '%' + @SearchTerm + '%'
        );
END
