CREATE PROCEDURE [security].[GetUsers]
AS
	Select [Id], [UserName], [Name], [Surname], [Email] from [security].[users]
RETURN 0
