USE [$(DatabaseName)]
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals dp WHERE dp.name = '$(SvcWebSite)')
BEGIN
    PRINT 'User [$(SvcWebSite)] created in [$(DatabaseName)] database.';
    CREATE USER [$(ServiceUserIdentity)] FROM LOGIN [$(ServiceUserIdentity)];
END
EXEC sys.sp_addrolemember @rolename = 'db_datareader', @membername = [$(SvcWebSite)];
PRINT 'User [$(SvcWebSite)] was granted db_datareader role permissions in [$(DatabaseName)] database.';
GO

