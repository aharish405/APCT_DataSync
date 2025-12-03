using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using Microsoft.Extensions.Configuration;

namespace DataSync.Data.Repositories
{
    public class DataExportRepository : IDataExportRepository
    {
        private readonly IConfiguration _configuration;

        public DataExportRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IEnumerable<dynamic>> GetDataAsync(ExportConfiguration config, DateTime fromDate, DateTime toDate)
        {
            // Construct connection string dynamically
            string connectionString;
            
            var credentials = _configuration.GetSection("DatabaseCredentials").Get<List<DatabaseCredential>>();
            var credential = credentials?.Find(c => c.ServerIP == config.DbServerIP && c.DbName == config.DbName);

            if (credential != null)
            {
                connectionString = $"Server={config.DbServerIP};Database={config.DbName};User Id={credential.UserId};Password={credential.Password};";
            }
            else
            {
                // Default to Windows Authentication
                connectionString = $"Server={config.DbServerIP};Database={config.DbName};Trusted_Connection=True;";
            }

            using (var connection = new SqlConnection(connectionString))
            {
                // Use brackets to prevent SQL injection via table/column names (basic protection)
                // In a real scenario, we should validate these against the schema
                var sql = $"SELECT * FROM [{config.TableName}] WHERE [{config.DateColumn}] BETWEEN @FromDate AND @ToDate";
                
                // Use Dapper's QueryAsync which streams results if buffered is false, but here we return IEnumerable
                // To truly stream, we might need to return IDataReader or use CommandBehavior.SequentialAccess
                // But Dapper buffers by default. For "huge" data, we should set buffered: false
                return await connection.QueryAsync(sql, new { FromDate = fromDate, ToDate = toDate }, commandTimeout: 300);
            }
        }

        private class DatabaseCredential
        {
            public string ServerIP { get; set; }
            public string DbName { get; set; }
            public string UserId { get; set; }
            public string Password { get; set; }
        }
    }
}
