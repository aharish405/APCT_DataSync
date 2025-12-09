using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DataSync.Core.Interfaces;

namespace DataSync.Data.Repositories
{
    public class DataCopyExecutionRepository : IDataCopyExecutionRepository
    {
        public async Task<int> GetSourceRecordCountAsync(string connectionString, string tableName, string customQuery)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                string sql;
                if (!string.IsNullOrWhiteSpace(customQuery))
                {
                    sql = $"SELECT COUNT(*) FROM [{tableName}] {customQuery}";
                }
                else
                {
                    sql = $"SELECT COUNT(*) FROM [{tableName}]";
                }
                
                return await connection.ExecuteScalarAsync<int>(sql, commandTimeout: 30);
            }
        }

        public async Task<IEnumerable<dynamic>> GetSourceDataAsync(string connectionString, string tableName, string customQuery, int offset, int batchSize)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                string sql;
                if (!string.IsNullOrWhiteSpace(customQuery))
                {
                    sql = $@"
                        SELECT * FROM [{tableName}] {customQuery}
                        ORDER BY (SELECT NULL)
                        OFFSET @Offset ROWS FETCH NEXT @BatchSize ROWS ONLY";
                }
                else
                {
                    sql = $@"
                        SELECT * FROM [{tableName}]
                        ORDER BY (SELECT NULL)
                        OFFSET @Offset ROWS FETCH NEXT @BatchSize ROWS ONLY";
                }
                
                return await connection.QueryAsync(sql, new { Offset = offset, BatchSize = batchSize }, commandTimeout: 60);
            }
        }

        public async Task<int> InsertBatchAsync(string connectionString, string tableName, IEnumerable<dynamic> data)
        {
            if (data == null || !data.Any())
                return 0;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                // Get column names from first record
                var firstRecord = data.First() as IDictionary<string, object>;
                var columns = firstRecord.Keys.ToList();
                var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
                var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
                
                var sql = $"INSERT INTO [{tableName}] ({columnList}) VALUES ({paramList})";
                
                try
                {
                    // Disable foreign key constraints to allow inserting data with FK references
                    await connection.ExecuteAsync($"ALTER TABLE [{tableName}] NOCHECK CONSTRAINT ALL", commandTimeout: 30);
                    
                    // Enable IDENTITY_INSERT to allow inserting into identity columns
                    await connection.ExecuteAsync($"SET IDENTITY_INSERT [{tableName}] ON", commandTimeout: 30);
                    
                    var result = await connection.ExecuteAsync(sql, data, commandTimeout: 120);
                    
                    // Disable IDENTITY_INSERT after insert
                    await connection.ExecuteAsync($"SET IDENTITY_INSERT [{tableName}] OFF", commandTimeout: 30);
                    
                    // Re-enable foreign key constraints
                    await connection.ExecuteAsync($"ALTER TABLE [{tableName}] CHECK CONSTRAINT ALL", commandTimeout: 30);
                    
                    return result;
                }
                catch
                {
                    // Make sure to turn off IDENTITY_INSERT and re-enable constraints even if insert fails
                    try
                    {
                        await connection.ExecuteAsync($"SET IDENTITY_INSERT [{tableName}] OFF", commandTimeout: 30);
                        await connection.ExecuteAsync($"ALTER TABLE [{tableName}] CHECK CONSTRAINT ALL", commandTimeout: 30);
                    }
                    catch { }
                    throw;
                }
            }
        }

        public async Task TruncateDestinationAsync(string connectionString, string tableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var sql = $"TRUNCATE TABLE [{tableName}]";
                await connection.ExecuteAsync(sql, commandTimeout: 30);
            }
        }

        public async Task<(bool Success, string ErrorMessage)> TestConnectionAsync(string connectionString)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return (connection.State == ConnectionState.Open, null);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<bool> TableExistsAsync(string connectionString, string tableName)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    var sql = @"
                        SELECT COUNT(*) 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_NAME = @TableName";
                    
                    var count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName });
                    return count > 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
