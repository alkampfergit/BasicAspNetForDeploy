using System.Configuration;
using TestWebApp.Core.Sql;

namespace TestWebApp.App_Start
{
    public static class Bootstrapper
    {
        public static void Start()
        {
            var connection = ConfigurationManager.ConnectionStrings["db"];
            DataAccess.SetConnectionString(connection.ConnectionString, connection.ProviderName);
        }
    }
}