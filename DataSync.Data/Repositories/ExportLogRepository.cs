using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using System.Linq;

namespace DataSync.Data.Repositories
{
    public class ExportLogRepository : IExportLogRepository
    {
        private readonly string _connectionString;

        public ExportLogRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<long> LogStartAsync(string appId, string tableName, DateTime fromDate, DateTime toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO DataSync_ExportLogs (AppId, TableName, FromDate, ToDate, Status)
                    VALUES (@AppId, @TableName, @FromDate, @ToDate, 'Started');
                    SELECT CAST(SCOPE_IDENTITY() as bigint)";
                return await connection.ExecuteScalarAsync<long>(sql, new { AppId = appId, TableName = tableName, FromDate = fromDate, ToDate = toDate });
            }
        }

        public async Task LogEndAsync(long logId, int recordsCount, string status, string errorMessage = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    UPDATE DataSync_ExportLogs 
                    SET RecordsCount = @RecordsCount, Status = @Status, ErrorMessage = @ErrorMessage
                    WHERE Id = @Id";
                await connection.ExecuteAsync(sql, new { Id = logId, RecordsCount = recordsCount, Status = status, ErrorMessage = errorMessage });
            }
        }

        public async Task<PagedResult<ExportLog>> GetLogsAsync(int pageNumber, int pageSize, string appId = null, string status = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_ExportLogs WHERE 1=1";
                var countSql = "SELECT COUNT(*) FROM DataSync_ExportLogs WHERE 1=1";
                var parameters = new DynamicParameters();

                if (!string.IsNullOrEmpty(appId))
                {
                    sql += " AND AppId LIKE @AppId";
                    countSql += " AND AppId LIKE @AppId";
                    parameters.Add("AppId", $"%{appId}%");
                }

                if (!string.IsNullOrEmpty(status))
                {
                    sql += " AND Status = @Status";
                    countSql += " AND Status = @Status";
                    parameters.Add("Status", status);
                }

                sql += " ORDER BY RequestTimestamp DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                parameters.Add("Offset", (pageNumber - 1) * pageSize);
                parameters.Add("PageSize", pageSize);

                var items = await connection.QueryAsync<ExportLog>(sql, parameters);
                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

                return new PagedResult<ExportLog>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }
    }
}
