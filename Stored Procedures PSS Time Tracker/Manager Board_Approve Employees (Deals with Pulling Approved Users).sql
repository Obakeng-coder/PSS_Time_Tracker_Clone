-- ================================================
-- Manager Board/Approve Employees
-- Deals With pulled employees and pulls them via Procedure tester
-- ================================================
CREATE PROCEDURE GetUsersNeedingApprovalCount
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