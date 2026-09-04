-- ==-- ================================================
-- Manager Board/ Employee Timesheets/ Persnoalised Employee
-- This Procedure deals with getting count of personalised employee data

-- This procedure shows only employees related to mager
-- ================================================
CREATE PROCEDURE GetManagerEmployeeTimeSheetsCount
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
