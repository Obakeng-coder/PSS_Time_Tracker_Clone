-- ================================================
-- GetEmployeeSummaryPaged: Manager Board/Employee Timesheets
-- For paginated employee summaries
-- This Procedure Paginates Employees
-- ================================================

CREATE PROCEDURE GetEmployeeSummaryPaged
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
