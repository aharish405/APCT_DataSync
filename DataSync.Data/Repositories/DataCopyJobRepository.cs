using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DataSync.Core.Interfaces;
using DataSync.Core.Models.DataCopy;

namespace DataSync.Data.Repositories
{
    public class DataCopyJobRepository : IDataCopyJobRepository
    {
        private readonly string _connectionString;

        public DataCopyJobRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> CreateJobAsync(DataCopyJob job)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO DataCopy_Jobs 
                    (ConfigId, Status, TriggerType, TotalRecords, ProcessedRecords, FailedRecords, 
                     ProgressPercentage, StartTime, EndTime, Duration, ErrorMessage, CreatedDate)
                    VALUES 
                    (@ConfigId, @Status, @TriggerType, @TotalRecords, @ProcessedRecords, @FailedRecords,
                     @ProgressPercentage, @StartTime, @EndTime, @Duration, @ErrorMessage, GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() as int)";
                return await connection.ExecuteScalarAsync<int>(sql, job);
            }
        }

        public async Task UpdateJobAsync(DataCopyJob job)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    UPDATE DataCopy_Jobs 
                    SET Status = @Status,
                        TotalRecords = @TotalRecords,
                        ProcessedRecords = @ProcessedRecords,
                        FailedRecords = @FailedRecords,
                        ProgressPercentage = @ProgressPercentage,
                        StartTime = @StartTime,
                        EndTime = @EndTime,
                        Duration = @Duration,
                        ErrorMessage = @ErrorMessage
                    WHERE Id = @Id";
                await connection.ExecuteAsync(sql, job);
            }
        }

        public async Task<DataCopyJob> GetJobByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT j.*, c.* 
                    FROM DataCopy_Jobs j
                    LEFT JOIN DataCopy_Configurations c ON j.ConfigId = c.Id
                    WHERE j.Id = @Id";
                
                var jobs = await connection.QueryAsync<DataCopyJob, DataCopyConfiguration, DataCopyJob>(
                    sql,
                    (job, config) =>
                    {
                        job.Configuration = config;
                        return job;
                    },
                    new { Id = id },
                    splitOn: "Id"
                );
                
                return jobs.FirstOrDefault();
            }
        }

        public async Task<IEnumerable<DataCopyJob>> GetJobsByConfigIdAsync(int configId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT j.*, c.* 
                    FROM DataCopy_Jobs j
                    LEFT JOIN DataCopy_Configurations c ON j.ConfigId = c.Id
                    WHERE j.ConfigId = @ConfigId
                    ORDER BY j.CreatedDate DESC";
                
                var jobs = await connection.QueryAsync<DataCopyJob, DataCopyConfiguration, DataCopyJob>(
                    sql,
                    (job, config) =>
                    {
                        job.Configuration = config;
                        return job;
                    },
                    new { ConfigId = configId },
                    splitOn: "Id"
                );
                
                return jobs;
            }
        }

        public async Task<IEnumerable<DataCopyJob>> GetRecentJobsAsync(int count)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = $@"
                    SELECT TOP {count} j.*, c.* 
                    FROM DataCopy_Jobs j
                    LEFT JOIN DataCopy_Configurations c ON j.ConfigId = c.Id
                    ORDER BY j.CreatedDate DESC";
                
                var jobs = await connection.QueryAsync<DataCopyJob, DataCopyConfiguration, DataCopyJob>(
                    sql,
                    (job, config) =>
                    {
                        job.Configuration = config;
                        return job;
                    },
                    splitOn: "Id"
                );
                
                return jobs;
            }
        }

        public async Task<IEnumerable<DataCopyJob>> GetActiveJobsAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT j.*, c.* 
                    FROM DataCopy_Jobs j
                    LEFT JOIN DataCopy_Configurations c ON j.ConfigId = c.Id
                    WHERE j.Status IN ('Pending', 'Running')
                    ORDER BY j.CreatedDate DESC";
                
                var jobs = await connection.QueryAsync<DataCopyJob, DataCopyConfiguration, DataCopyJob>(
                    sql,
                    (job, config) =>
                    {
                        job.Configuration = config;
                        return job;
                    },
                    splitOn: "Id"
                );
                
                return jobs;
            }
        }

        public async Task AddJobLogAsync(DataCopyJobLog log)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO DataCopy_JobLogs (JobId, LogLevel, Message, LogTime)
                    VALUES (@JobId, @LogLevel, @Message, GETDATE())";
                await connection.ExecuteAsync(sql, log);
            }
        }

        public async Task<IEnumerable<DataCopyJobLog>> GetJobLogsAsync(int jobId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT * FROM DataCopy_JobLogs 
                    WHERE JobId = @JobId 
                    ORDER BY LogTime DESC";
                return await connection.QueryAsync<DataCopyJobLog>(sql, new { JobId = jobId });
            }
        }
    }
}
