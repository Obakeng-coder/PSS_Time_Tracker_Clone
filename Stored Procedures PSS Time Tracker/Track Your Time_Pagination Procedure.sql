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

