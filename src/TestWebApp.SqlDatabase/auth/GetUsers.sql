CREATE PROCEDURE [auth].[GetUsers]
AS
	Select [Id], [UserName], [Name], [Surname], [Email] from [auth].[users]
RETURN 0
