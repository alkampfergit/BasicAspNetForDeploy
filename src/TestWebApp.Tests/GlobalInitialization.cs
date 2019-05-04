using Microsoft.SqlServer.Dac;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using TestWebApp.Core.Sql;

namespace TestWebApp.Tests
{
    [SetUpFixture]
    public class GlobalInitialization
    {
        [OneTimeSetUp]
        public void GlobalAssemblyTestInitialization()
        {
            var connectionString = Environment.GetEnvironmentVariable("TEST_SQLINSTANCE");
            var testDatabaseName = "TestWebAppTestDatabase";
            if (String.IsNullOrEmpty(connectionString))
            {
                var connection = ConfigurationManager.ConnectionStrings["testConnection"];
                connectionString = connection.ConnectionString;
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            builder.InitialCatalog = testDatabaseName;
            DataAccess.SetConnectionString(builder.ToString(), "System.Data.SqlClient");

            //Now we want to generate the schema directly from the database project.
            var svc = new DacServices(connectionString);

            //need to locate the file.
            var dacfile = @"..\artifacts\TestWebApp.SqlDatabase.dacpac";

            try
            {
                builder.InitialCatalog = "master";
                var masterConnection = new ConnectionStringSettings("master", builder.ToString(), "System.Data.SqlClient");
                DataAccess.ExecuteNonQueryOutsideTransaction(masterConnection, $"alter database {testDatabaseName} set single_user with rollback immediate", false);
                DataAccess.ExecuteNonQueryOutsideTransaction(masterConnection, $"DROP DATABASE {testDatabaseName}", false);
            }
            catch (SqlException ex)
            {
                //ignore the exception, maybe the database was not there.
            }
            var fileInfo = new FileInfo(dacfile);

            svc.Deploy(
                DacPackage.Load(fileInfo.FullName),
                "TestWebAppTestDatabase",
                false,
                new DacDeployOptions()
                {

                }
                );
        }
    }
}
