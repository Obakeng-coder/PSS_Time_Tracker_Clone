-- ================================================
-- This Procedure is for Paging and using Pagination for "Employee Timesheet"
-- This page works when u click the hyperlink and it navigates to Personalised Employee
-- This Procedure deals with Pagination 

--This procedure shows only employees related to mager
-- ================================================
CREATE PROCEDURE GetManagerEmployeeTimeSheetsPaged
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
