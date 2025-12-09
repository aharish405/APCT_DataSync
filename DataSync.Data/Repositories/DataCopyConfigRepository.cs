using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using DataSync.Core.Models.DataCopy;

namespace DataSync.Data.Repositories
{
    public class DataCopyConfigRepository : IDataCopyConfigRepository
    {
        private readonly string _connectionString;

        public DataCopyConfigRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<DataCopyConfiguration> GetConfigurationByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataCopy_Configurations WHERE Id = @Id";
                return await connection.QueryFirstOrDefaultAsync<DataCopyConfiguration>(sql, new { Id = id });
            }
        }

        public async Task<IEnumerable<DataCopyConfiguration>> GetAllConfigurationsAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataCopy_Configurations ORDER BY ConfigName";
                return await connection.QueryAsync<DataCopyConfiguration>(sql);
            }
        }

        public async Task<PagedResult<DataCopyConfiguration>> GetAllConfigurationsPagedAsync(int pageNumber, int pageSize, string configName = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataCopy_Configurations WHERE 1=1";
                var countSql = "SELECT COUNT(*) FROM DataCopy_Configurations WHERE 1=1";
                var parameters = new DynamicParameters();

                if (!string.IsNullOrEmpty(configName))
                {
                    sql += " AND ConfigName LIKE @ConfigName";
                    countSql += " AND ConfigName LIKE @ConfigName";
                    parameters.Add("ConfigName", $"%{configName}%");
                }

                sql += " ORDER BY Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                parameters.Add("Offset", (pageNumber - 1) * pageSize);
                parameters.Add("PageSize", pageSize);

                var items = await connection.QueryAsync<DataCopyConfiguration>(sql, parameters);
                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

                return new PagedResult<DataCopyConfiguration>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }

        public async Task<int> AddConfigurationAsync(DataCopyConfiguration config)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO DataCopy_Configurations 
                    (ConfigName, SourceDbServerIP, SourceDbName, SourceTableName, SourceCustomQuery,
                     DestDbServerIP, DestDbName, DestTableName, TruncateBeforeCopy, BatchSize,
                     IsScheduled, ScheduleCron, Enabled, CreatedDate, ModifiedDate)
                    VALUES 
                    (@ConfigName, @SourceDbServerIP, @SourceDbName, @SourceTableName, @SourceCustomQuery,
                     @DestDbServerIP, @DestDbName, @DestTableName, @TruncateBeforeCopy, @BatchSize,
                     @IsScheduled, @ScheduleCron, @Enabled, GETDATE(), GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() as int)";
                return await connection.ExecuteScalarAsync<int>(sql, config);
            }
        }

        public async Task UpdateConfigurationAsync(DataCopyConfiguration config)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    UPDATE DataCopy_Configurations 
                    SET ConfigName = @ConfigName,
                        SourceDbServerIP = @SourceDbServerIP,
                        SourceDbName = @SourceDbName,
                        SourceTableName = @SourceTableName,
                        SourceCustomQuery = @SourceCustomQuery,
                        DestDbServerIP = @DestDbServerIP,
                        DestDbName = @DestDbName,
                        DestTableName = @DestTableName,
                        TruncateBeforeCopy = @TruncateBeforeCopy,
                        BatchSize = @BatchSize,
                        IsScheduled = @IsScheduled,
                        ScheduleCron = @ScheduleCron,
                        Enabled = @Enabled,
                        ModifiedDate = GETDATE()
                    WHERE Id = @Id";
                await connection.ExecuteAsync(sql, config);
            }
        }

        public async Task DeleteConfigurationAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "DELETE FROM DataCopy_Configurations WHERE Id = @Id";
                await connection.ExecuteAsync(sql, new { Id = id });
            }
        }

        public async Task<(bool Success, string Message)> ValidateConfigurationAsync(DataCopyConfiguration config)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(config.ConfigName))
                return (false, "Configuration name is required.");

            if (string.IsNullOrWhiteSpace(config.SourceDbServerIP) || string.IsNullOrWhiteSpace(config.SourceDbName) || string.IsNullOrWhiteSpace(config.SourceTableName))
                return (false, "Source database configuration is incomplete.");

            if (string.IsNullOrWhiteSpace(config.DestDbServerIP) || string.IsNullOrWhiteSpace(config.DestDbName) || string.IsNullOrWhiteSpace(config.DestTableName))
                return (false, "Destination database configuration is incomplete.");

            if (config.BatchSize <= 0)
                return (false, "Batch size must be greater than 0.");

            // Validate custom query if provided
            if (!string.IsNullOrWhiteSpace(config.SourceCustomQuery))
            {
                var query = config.SourceCustomQuery.Trim().ToUpper();
                
                // Block destructive keywords
                var forbiddenKeywords = new[] { "DELETE", "UPDATE", "INSERT", "DROP", "TRUNCATE", "ALTER", "CREATE", "GRANT", "REVOKE", "EXEC", "EXECUTE" };
                foreach (var keyword in forbiddenKeywords)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(query, $@"\b{keyword}\b"))
                    {
                        return (false, $"Custom query contains forbidden keyword: {keyword}");
                    }
                }

                // Block semicolons (statement chaining)
                if (query.Contains(";"))
                {
                    return (false, "Custom query cannot contain semicolons (statement chaining not allowed).");
                }
            }

            return (true, "Configuration is valid.");
        }
    }
}
