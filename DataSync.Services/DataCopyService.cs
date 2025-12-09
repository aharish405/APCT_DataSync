using System;
using System.Linq;
using System.Threading.Tasks;
using DataSync.Core.Interfaces;
using DataSync.Core.Models.DataCopy;
using DataSync.Data.Helpers;

namespace DataSync.Services
{
    public class DataCopyService : IDataCopyService
    {
        private readonly IDataCopyConfigRepository _configRepo;
        private readonly IDataCopyJobRepository _jobRepo;
        private readonly IDataCopyExecutionRepository _execRepo;
        private readonly ConnectionStringHelper _connHelper;

        public DataCopyService(
            IDataCopyConfigRepository configRepo,
            IDataCopyJobRepository jobRepo,
            IDataCopyExecutionRepository execRepo,
            ConnectionStringHelper connHelper)
        {
            _configRepo = configRepo;
            _jobRepo = jobRepo;
            _execRepo = execRepo;
            _connHelper = connHelper;
        }

        public async Task<(bool Success, string Message, int JobId)> ExecuteCopyJobAsync(int configId, DataCopyTriggerType triggerType)
        {
            try
            {
                // Get configuration
                var config = await _configRepo.GetConfigurationByIdAsync(configId);
                if (config == null)
                    return (false, "Configuration not found.", 0);

                if (!config.Enabled)
                    return (false, "Configuration is disabled.", 0);

                // Validate configuration
                var validation = await _configRepo.ValidateConfigurationAsync(config);
                if (!validation.Success)
                    return (false, validation.Message, 0);

                // Create job record
                var job = new DataCopyJob
                {
                    ConfigId = configId,
                    Status = DataCopyJobStatus.Pending,
                    TriggerType = triggerType,
                    ProcessedRecords = 0,
                    FailedRecords = 0,
                    ProgressPercentage = 0
                };

                var jobId = await _jobRepo.CreateJobAsync(job);
                job.Id = jobId;

                // Execute copy in background (fire and forget)
                _ = Task.Run(async () => await ExecuteCopyAsync(job, config));

                return (true, "Copy job started successfully.", jobId);
            }
            catch (Exception ex)
            {
                return (false, $"Error starting copy job: {ex.Message}", 0);
            }
        }

        private async Task ExecuteCopyAsync(DataCopyJob job, DataCopyConfiguration config)
        {
            try
            {
                // Update job status to Running
                job.Status = DataCopyJobStatus.Running;
                job.StartTime = DateTime.Now;
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(job.Id, DataCopyLogLevel.Info, "Copy job started");

                // Build connection strings
                var sourceConnString = _connHelper.BuildConnectionString(config.SourceDbServerIP, config.SourceDbName);
                var destConnString = _connHelper.BuildConnectionString(config.DestDbServerIP, config.DestDbName);

                // Test connections
                await LogAsync(job.Id, DataCopyLogLevel.Info, "Testing source connection...");
                var sourceTest = await _execRepo.TestConnectionAsync(sourceConnString);
                if (!sourceTest.Success)
                {
                    throw new Exception($"Failed to connect to source database: {sourceTest.ErrorMessage}");
                }

                await LogAsync(job.Id, DataCopyLogLevel.Info, "Testing destination connection...");
                var destTest = await _execRepo.TestConnectionAsync(destConnString);
                if (!destTest.Success)
                {
                    throw new Exception($"Failed to connect to destination database: {destTest.ErrorMessage}");
                }

                // Verify source table exists
                await LogAsync(job.Id, DataCopyLogLevel.Info, "Verifying source table exists...");
                if (!await _execRepo.TableExistsAsync(sourceConnString, config.SourceTableName))
                {
                    throw new Exception($"Source table '{config.SourceTableName}' does not exist");
                }

                // Verify destination table exists
                await LogAsync(job.Id, DataCopyLogLevel.Info, "Verifying destination table exists...");
                if (!await _execRepo.TableExistsAsync(destConnString, config.DestTableName))
                {
                    throw new Exception($"Destination table '{config.DestTableName}' does not exist");
                }

                // Truncate destination if configured
                if (config.TruncateBeforeCopy)
                {
                    await LogAsync(job.Id, DataCopyLogLevel.Info, "Truncating destination table...");
                    await _execRepo.TruncateDestinationAsync(destConnString, config.DestTableName);
                }

                // Get total record count
                await LogAsync(job.Id, DataCopyLogLevel.Info, "Counting source records...");
                var totalRecords = await _execRepo.GetSourceRecordCountAsync(sourceConnString, config.SourceTableName, config.SourceCustomQuery);
                job.TotalRecords = totalRecords;
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(job.Id, DataCopyLogLevel.Info, $"Total records to copy: {totalRecords}");

                if (totalRecords == 0)
                {
                    await LogAsync(job.Id, DataCopyLogLevel.Warning, "No records found to copy");
                    job.Status = DataCopyJobStatus.Completed;
                    job.EndTime = DateTime.Now;
                    job.Duration = (int)(job.EndTime.Value - job.StartTime.Value).TotalSeconds;
                    job.ProgressPercentage = 100;
                    await _jobRepo.UpdateJobAsync(job);
                    return;
                }

                // Copy data in batches
                var batchSize = config.BatchSize;
                var offset = 0;
                var processedRecords = 0;

                while (offset < totalRecords)
                {
                    try
                    {
                        await LogAsync(job.Id, DataCopyLogLevel.Info, $"Fetching batch: offset {offset}, size {batchSize}");
                        var batch = await _execRepo.GetSourceDataAsync(sourceConnString, config.SourceTableName, config.SourceCustomQuery, offset, batchSize);
                        var batchList = batch.ToList();

                        if (batchList.Any())
                        {
                            await LogAsync(job.Id, DataCopyLogLevel.Info, $"Inserting {batchList.Count} records...");
                            var inserted = await _execRepo.InsertBatchAsync(destConnString, config.DestTableName, batchList);
                            processedRecords += inserted;

                            // Update progress
                            job.ProcessedRecords = processedRecords;
                            job.ProgressPercentage = (decimal)processedRecords / totalRecords * 100;
                            await _jobRepo.UpdateJobAsync(job);
                        }

                        offset += batchSize;
                    }
                    catch (Exception batchEx)
                    {
                        // Get actual batch size for failed count
                        var actualBatchSize = Math.Min(batchSize, totalRecords - offset);
                        await LogAsync(job.Id, DataCopyLogLevel.Error, $"Batch error at offset {offset}: {batchEx.Message}");
                        job.FailedRecords += actualBatchSize;
                        offset += batchSize; // Skip failed batch and continue
                    }
                }

                // Mark as completed
                job.Status = DataCopyJobStatus.Completed;
                job.EndTime = DateTime.Now;
                job.Duration = (int)(job.EndTime.Value - job.StartTime.Value).TotalSeconds;
                job.ProgressPercentage = 100;
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(job.Id, DataCopyLogLevel.Info, $"Copy completed. Processed: {processedRecords}, Failed: {job.FailedRecords}");
            }
            catch (Exception ex)
            {
                job.Status = DataCopyJobStatus.Failed;
                job.EndTime = DateTime.Now;
                job.Duration = job.StartTime.HasValue ? (int)(job.EndTime.Value - job.StartTime.Value).TotalSeconds : 0;
                job.ErrorMessage = ex.Message;
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(job.Id, DataCopyLogLevel.Error, $"Copy failed: {ex.Message}");
            }
        }

        public async Task<DataCopyJob> GetJobStatusAsync(int jobId)
        {
            return await _jobRepo.GetJobByIdAsync(jobId);
        }

        public async Task<(bool Success, string Message)> ValidateConfigurationAsync(int configId)
        {
            try
            {
                var config = await _configRepo.GetConfigurationByIdAsync(configId);
                if (config == null)
                    return (false, "Configuration not found.");

                // Basic validation
                var validation = await _configRepo.ValidateConfigurationAsync(config);
                if (!validation.Success)
                    return validation;

                // Test connections
                var sourceConnString = _connHelper.BuildConnectionString(config.SourceDbServerIP, config.SourceDbName);
                var destConnString = _connHelper.BuildConnectionString(config.DestDbServerIP, config.DestDbName);

                var sourceTest = await _execRepo.TestConnectionAsync(sourceConnString);
                if (!sourceTest.Success)
                    return (false, $"Cannot connect to source database: {sourceTest.ErrorMessage}. Check server IP, database name, and credentials in appsettings.json.");

                var destTest = await _execRepo.TestConnectionAsync(destConnString);
                if (!destTest.Success)
                    return (false, $"Cannot connect to destination database: {destTest.ErrorMessage}. Check server IP, database name, and credentials in appsettings.json.");

                // Verify tables exist
                if (!await _execRepo.TableExistsAsync(sourceConnString, config.SourceTableName))
                    return (false, $"Source table '{config.SourceTableName}' does not exist.");

                if (!await _execRepo.TableExistsAsync(destConnString, config.DestTableName))
                    return (false, $"Destination table '{config.DestTableName}' does not exist.");

                return (true, "Configuration is valid and ready to use.");
            }
            catch (Exception ex)
            {
                return (false, $"Validation error: {ex.Message}");
            }
        }

        public async Task CancelJobAsync(int jobId)
        {
            var job = await _jobRepo.GetJobByIdAsync(jobId);
            if (job != null && (job.Status == DataCopyJobStatus.Pending || job.Status == DataCopyJobStatus.Running))
            {
                job.Status = DataCopyJobStatus.Cancelled;
                job.EndTime = DateTime.Now;
                job.Duration = job.StartTime.HasValue ? (int)(job.EndTime.Value - job.StartTime.Value).TotalSeconds : 0;
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(jobId, DataCopyLogLevel.Warning, "Job cancelled by user");
            }
        }

        private async Task LogAsync(int jobId, DataCopyLogLevel level, string message)
        {
            await _jobRepo.AddJobLogAsync(new DataCopyJobLog
            {
                JobId = jobId,
                LogLevel = level,
                Message = message
            });
        }
    }
}
