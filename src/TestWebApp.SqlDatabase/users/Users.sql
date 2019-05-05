CREATE TABLE [dbo].[Users]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [UserName] VARCHAR(80) NOT NULL, 
    [Name] NVARCHAR(50) NULL, 
    [Surname] NVARCHAR(50) NULL, 
    [Email] VARCHAR(100) NOT NULL
)

GO

CREATE UNIQUE INDEX [IX_Users_USerName] ON [dbo].[Users] ([UserName])
