CREATE PROCEDURE [security].[adduser]
	@username VARCHAR(80)
	,@name NVARCHAR(50)
	,@surname NVARCHAR(50)
	,@email VARCHAR(100)
	,@identity INT output
AS
	INSERT INTO [security].[Users]
           ([UserName]
           ,[Name]
           ,[Surname]
           ,[email])
     VALUES
           (@username
           ,@name
           ,@surname
           ,@email);

SELECT @identity = SCOPE_IDENTITY(); 

