-- ---------------------------------------------------------------------
-- Source: Manager Board_Employee Timesheets_Personalised Employee (Deals with Pagination).sql
-- ---------------------------------------------------------------------
-- ================================================
-- This Procedure is for Paging and using Pagination for "Employee Timesheet"
-- This page works when u click the hyperlink and it navigates to Personalised Employee
-- This Procedure deals with Pagination
--
-- This procedure shows every employee reporting to the manager (Users, filtered by
-- SupervisorEmail) via a LEFT JOIN to TimeTracker - an employee with zero timesheet rows
-- (a new hire, or anyone who has only ever used the self-service leave-flag with no raw
-- worked-day entry) still appears, with TimesheetCount=0 and LatestEntry=NULL, instead of
-- being silently absent from the Manager Board. Previously this was an INNER JOIN starting
-- FROM TimeTracker, which excluded exactly those employees.
--
-- @SearchTerm now actually filters the list (previously accepted by neither this proc nor
-- its caller - the Manager Board's Search box was a no-op).
-- ================================================
CREATE OR ALTER PROCEDURE GetManagerEmployeeTimeSheetsPaged
    @SupervisorEmail NVARCHAR(256),
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @SearchTerm NVARCHAR(256) = NULL,
    @PageNumber INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH EmployeeSummary AS (
        SELECT
            u.AzureAdUserId,
            u.EmployeeName,
            u.EmployeeSurname,
            COUNT(t.TimeTrackerId) AS TimesheetCount,
            MAX(t.DateOfEntry) AS LatestEntry
        FROM Users u
        LEFT JOIN TimeTracker t
            ON t.AzureAdUserId = u.AzureAdUserId
            AND (@StartDate IS NULL OR t.DateOfEntry >= @StartDate)
            AND (@EndDate IS NULL OR t.DateOfEntry <= @EndDate)
        WHERE u.SupervisorEmail = @SupervisorEmail
            AND (
                @SearchTerm IS NULL OR @SearchTerm = '' OR
                (u.EmployeeName + ' ' + u.EmployeeSurname) LIKE '%' + @SearchTerm + '%' OR
                u.EmployeeName LIKE '%' + @SearchTerm + '%' OR
                u.EmployeeSurname LIKE '%' + @SearchTerm + '%'
            )
        GROUP BY u.AzureAdUserId, u.EmployeeName, u.EmployeeSurname
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
