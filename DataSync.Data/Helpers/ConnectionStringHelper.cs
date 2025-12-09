using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace DataSync.Data.Helpers
{
    public class ConnectionStringHelper
    {
        private readonly IConfiguration _configuration;

        public ConnectionStringHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string BuildConnectionString(string serverIP, string dbName)
        {
            // Try to find credentials in appsettings.json
            var credentials = _configuration.GetSection("DatabaseCredentials").Get<List<DatabaseCredential>>();
            var credential = credentials?.FirstOrDefault(c => c.ServerIP == serverIP && c.DbName == dbName);

            if (credential != null)
            {
                // Use SQL Authentication with credentials from appsettings
                return $"Server={serverIP};Database={dbName};User Id={credential.UserId};Password={credential.Password};TrustServerCertificate=True;";
            }
            else
            {
                // Fall back to Windows Authentication
                return $"Server={serverIP};Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;";
            }
        }
    }

    public class DatabaseCredential
    {
        public string ServerIP { get; set; }
        public string DbName { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
    }
}
