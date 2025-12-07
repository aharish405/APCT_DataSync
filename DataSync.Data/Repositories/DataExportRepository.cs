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
                // Check for Custom Query
                string sql;
                if (!string.IsNullOrWhiteSpace(config.CustomQuery))
                {
                    // Security Check
                    var safetyCheck = ValidateQuerySafety(config.CustomQuery);
                    if (!safetyCheck.IsSafe)
                    {
                         throw new InvalidOperationException($"Query Security Validation Failed: {safetyCheck.Message}");
                    }
                    sql = config.CustomQuery;
                }
                else
                {
                    // Use brackets to prevent SQL injection via table/column names (basic protection)
                    sql = $"SELECT * FROM [{config.TableName}] WHERE [{config.DateColumn}] BETWEEN @FromDate AND @ToDate";
                }
                
                // Use Dapper's QueryAsync which streams results if buffered is false, but here we return IEnumerable
                // To truly stream, we might need to return IDataReader or use CommandBehavior.SequentialAccess
                // But Dapper buffers by default. For "huge" data, we should set buffered: false
                return await connection.QueryAsync(sql, new { FromDate = fromDate, ToDate = toDate }, commandTimeout: 300);
            }
        }

        public async Task<(bool Success, string Message, int Count)> ValidateQueryAsync(ExportConfiguration config, DateTime? testFromDate = null, DateTime? testToDate = null)
        {
            try
            {
                // Construct connection string dynamically (Reuse logic)
                string connectionString;
                var credentials = _configuration.GetSection("DatabaseCredentials").Get<List<DatabaseCredential>>();
                var credential = credentials?.Find(c => c.ServerIP == config.DbServerIP && c.DbName == config.DbName);

                if (credential != null)
                {
                    connectionString = $"Server={config.DbServerIP};Database={config.DbName};User Id={credential.UserId};Password={credential.Password};";
                }
                else
                {
                    connectionString = $"Server={config.DbServerIP};Database={config.DbName};Trusted_Connection=True;";
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    string sql;
                    if (!string.IsNullOrWhiteSpace(config.CustomQuery))
                    {
                        // Security Check
                        var safetyCheck = ValidateQuerySafety(config.CustomQuery);
                        if (!safetyCheck.IsSafe)
                        {
                             return (false, safetyCheck.Message, 0);
                        }
                        // Wrap custom query to get count
                        sql = $"SELECT COUNT(*) FROM ({config.CustomQuery}) AS ValidQuery";
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(config.TableName) || string.IsNullOrWhiteSpace(config.DateColumn))
                        {
                            return (false, "Table Name and Date Column are required for standard queries.", 0);
                        }
                        sql = $"SELECT COUNT(*) FROM [{config.TableName}] WHERE [{config.DateColumn}] BETWEEN @FromDate AND @ToDate";
                    }

                    // Use provided test dates or default to last 30 days
                    var toDate = testToDate ?? DateTime.Now;
                    var fromDate = testFromDate ?? toDate.AddDays(-30);

                    var count = await connection.ExecuteScalarAsync<int>(sql, new { FromDate = fromDate, ToDate = toDate }, commandTimeout: 30);
                    return (true, "Query is valid.", count);
                }
            }
            catch (SqlException ex)
            {
                return (false, $"SQL Error: {ex.Message}", 0);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}", 0);
            }
        }

        private (bool IsSafe, string Message) ValidateQuerySafety(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return (true, "");

            string upperSql = sql.Trim().ToUpperInvariant();

            // Rule 1: Must Start with SELECT or WITH
            if (!upperSql.StartsWith("SELECT") && !upperSql.StartsWith("WITH"))
            {
                return (false, "Security Alert: Query must start with SELECT or WITH.");
            }

            // Rule 2: No destructive commands
            // Using Regex with word boundaries \b to avoid matching columns like 'UPDATE_DATE'
            var forbiddenKeywords = new[] { "DELETE", "UPDATE", "INSERT", "DROP", "TRUNCATE", "ALTER", "CREATE", "MERGE", "GRANT", "REVOKE", "EXEC", "EXECUTE" };
            foreach (var keyword in forbiddenKeywords)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(upperSql, $@"\b{keyword}\b"))
                {
                    return (false, $"Security Alert: Query contains forbidden keyword: {keyword}");
                }
            }

            // Rule 3: No chaining
            if (sql.Contains(";"))
            {
                return (false, "Security Alert: Query chaining (;) is not allowed.");
            }

            return (true, string.Empty);
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
