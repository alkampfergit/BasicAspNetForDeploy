using Microsoft.SqlServer.Dac;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
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
            var dacfile = FindDacPack();

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

        private static string FindDacPack()
        {
            var actualDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while(true)
            {
                var files =actualDirectory.GetFiles("TestWebApp.sln");
                if (files.Length > 0)
                {
                    return Path.Combine(actualDirectory.Parent.FullName, "artifacts", "TestWebApp.SqlDatabase.dacpac");
                }
                else
                {
                    actualDirectory = actualDirectory.Parent;
                }
            }
        }
    }
}
