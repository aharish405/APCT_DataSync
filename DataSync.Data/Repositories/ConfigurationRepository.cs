using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using System.Linq;

namespace DataSync.Data.Repositories
{
    public class ConfigurationRepository : IConfigurationRepository
    {
        private readonly string _connectionString;

        public ConfigurationRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<ExportConfiguration> GetConfigurationAsync(string appId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_ExportConfigurations WHERE AppId = @AppId";
                return await connection.QueryFirstOrDefaultAsync<ExportConfiguration>(sql, new { AppId = appId });
            }
        }

        public async Task<ExportConfiguration> GetConfigurationByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_ExportConfigurations WHERE Id = @Id";
                return await connection.QueryFirstOrDefaultAsync<ExportConfiguration>(sql, new { Id = id });
            }
        }

        public async Task<ExportConfiguration> GetConfigurationAsync(string appName, string dbName, string tableName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_ExportConfigurations WHERE AppName = @AppName AND DbName = @DbName AND TableName = @TableName";
                return await connection.QueryFirstOrDefaultAsync<ExportConfiguration>(sql, new { AppName = appName, DbName = dbName, TableName = tableName });
            }
        }

        public async Task<IEnumerable<ConfigDetailsResponse>> GetAllExportTablesAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT AppId, AppName, DbName, TableName FROM DataSync_ExportConfigurations";
                return await connection.QueryAsync<ConfigDetailsResponse>(sql);
            }
        }

        public async Task<PagedResult<ExportConfiguration>> GetAllConfigurationsAsync(int pageNumber, int pageSize, string appName = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_ExportConfigurations WHERE 1=1";
                var countSql = "SELECT COUNT(*) FROM DataSync_ExportConfigurations WHERE 1=1";
                var parameters = new DynamicParameters();

                if (!string.IsNullOrEmpty(appName))
                {
                    sql += " AND AppName LIKE @AppName";
                    countSql += " AND AppName LIKE @AppName";
                    parameters.Add("AppName", $"%{appName}%");
                }

                sql += " ORDER BY Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                parameters.Add("Offset", (pageNumber - 1) * pageSize);
                parameters.Add("PageSize", pageSize);

                var items = await connection.QueryAsync<ExportConfiguration>(sql, parameters);
                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

                return new PagedResult<ExportConfiguration>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }

        public async Task<int> AddConfigurationAsync(ExportConfiguration config)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO DataSync_ExportConfigurations (AppId, AppName, DbServerIP, DbName, TableName, DateColumn, CustomQuery, Enabled)
                    VALUES (@AppId, @AppName, @DbServerIP, @DbName, @TableName, @DateColumn, @CustomQuery, @Enabled);
                    SELECT CAST(SCOPE_IDENTITY() as int)";
                return await connection.ExecuteScalarAsync<int>(sql, config);
            }
        }

        public async Task UpdateConfigurationAsync(ExportConfiguration config)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    UPDATE DataSync_ExportConfigurations 
                    SET AppId = @AppId, AppName = @AppName, DbServerIP = @DbServerIP, 
                        DbName = @DbName, TableName = @TableName, DateColumn = @DateColumn, 
                        CustomQuery = @CustomQuery, Enabled = @Enabled
                    WHERE Id = @Id";
                await connection.ExecuteAsync(sql, config);
            }
        }

        public async Task DeleteConfigurationAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "DELETE FROM DataSync_ExportConfigurations WHERE Id = @Id";
                await connection.ExecuteAsync(sql, new { Id = id });
            }
        }

        public async Task DeleteConfigurationsAsync(IEnumerable<int> ids)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "DELETE FROM DataSync_ExportConfigurations WHERE Id IN @Ids";
                await connection.ExecuteAsync(sql, new { Ids = ids });
            }
        }

        public async Task AddConfigurationsAsync(IEnumerable<ExportConfiguration> configs)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    var sql = @"
                        INSERT INTO DataSync_ExportConfigurations (AppId, AppName, DbServerIP, DbName, TableName, DateColumn, CustomQuery, Enabled)
                        VALUES (@AppId, @AppName, @DbServerIP, @DbName, @TableName, @DateColumn, @CustomQuery, @Enabled)";
                    await connection.ExecuteAsync(sql, configs, transaction: transaction);
                    transaction.Commit();
                }
            }
        }
    }
}
