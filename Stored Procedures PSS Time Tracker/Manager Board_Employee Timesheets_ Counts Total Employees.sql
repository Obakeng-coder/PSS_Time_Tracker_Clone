-- ================================================
-- GetEmployeeSummaryCount: to count total matching results for pagination
-- Manager Board/ Employee Timesheets
-- Displays list of all employees and pulls employee details
-- This page is where employees click employee name hyperlink
-- ================================================

CREATE PROCEDURE GetEmployeeSummaryCount
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
