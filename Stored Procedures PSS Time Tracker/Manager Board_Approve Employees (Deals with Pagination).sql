-- ================================================
-- Manager Board/ Requires admin approval
-- Deal with Pagination for Paged employees requiring approval
-- sdsad
-- ================================================
CREATE PROCEDURE GetUsersNeedingApprovalPaged
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