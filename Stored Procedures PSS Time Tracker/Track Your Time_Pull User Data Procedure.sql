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
