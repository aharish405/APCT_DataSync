using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DataSync.Core.Interfaces;
using DataSync.Core.Models.DataCopy;
using DataSync.Data;
using DataSync.Data.Helpers;

namespace DataSync.Services
{
    public class DataCopyService : IDataCopyService
    {
        private readonly IDataCopyConfigRepository _configRepo;
        private readonly IDataCopyJobRepository _jobRepo;
        private readonly IDataCopyExecutionRepository _execRepo;
        private readonly ConnectionStringHelper _connHelper;
        private readonly DataSyncDbContext _dbContext;

        public DataCopyService(
            IDataCopyConfigRepository configRepo,
            IDataCopyJobRepository jobRepo,
            IDataCopyExecutionRepository execRepo,
            ConnectionStringHelper connHelper,
            DataSyncDbContext dbContext)
        {
            _configRepo = configRepo;
            _jobRepo = jobRepo;
            _execRepo = execRepo;
            _connHelper = connHelper;
            _dbContext = dbContext;
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
                job.CanResume = true; // Enable resume capability
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(job.Id, DataCopyLogLevel.Info, job.RetryCount > 0 ? $"Resuming copy job (Retry #{job.RetryCount})" : "Copy job started");

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

                // Truncate destination if configured (only on first run, not on resume)
                if (config.TruncateBeforeCopy && job.LastSuccessfulOffset == 0)
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
                    job.CanResume = false;
                    await _jobRepo.UpdateJobAsync(job);
                    return;
                }


                // Copy data in batches - Resume from checkpoint if available
                var batchSize = config.BatchSize;
                var offset = job.LastSuccessfulOffset; // Resume from last checkpoint
                var processedRecords = job.ProcessedRecords; // Continue from last count

                if (offset > 0)
                {
                    await LogAsync(job.Id, DataCopyLogLevel.Info, $"Resuming from checkpoint: offset {offset}, already processed {processedRecords} records");
                }

                while (offset < totalRecords)
                {
                    List<dynamic> batchList = null;
                    try
                    {
                        await LogAsync(job.Id, DataCopyLogLevel.Info, $"Fetching batch: offset {offset}, size {batchSize}");
                        var batch = await _execRepo.GetSourceDataAsync(sourceConnString, config.SourceTableName, config.SourceCustomQuery, offset, batchSize);
                        batchList = batch.ToList();

                        if (batchList.Any())
                        {
                            await LogAsync(job.Id, DataCopyLogLevel.Info, $"Inserting {batchList.Count} records...");
                            var inserted = await _execRepo.InsertBatchAsync(destConnString, config.DestTableName, batchList);
                            processedRecords += inserted;

                            // Save checkpoint after successful batch
                            job.ProcessedRecords = processedRecords;
                            job.LastSuccessfulOffset = offset + batchSize;
                            job.ProgressPercentage = (decimal)processedRecords / totalRecords * 100;
                            await _jobRepo.UpdateJobAsync(job);
                            await LogAsync(job.Id, DataCopyLogLevel.Info, $"Checkpoint saved at offset {job.LastSuccessfulOffset}");
                        }

                        offset += batchSize;
                    }
                    catch (Exception batchEx)
                    {
                        var actualBatchSize = Math.Min(batchSize, totalRecords - offset);
                        
                        // Classify error type
                        if (IsTransientError(batchEx))
                        {
                            // Transient errors (connection, timeout, deadlock) - fail job, allow resume
                            await LogAsync(job.Id, DataCopyLogLevel.Error, $"Transient error at offset {offset}: {batchEx.Message}");
                            await LogAsync(job.Id, DataCopyLogLevel.Info, $"Job will fail and can be resumed from offset {job.LastSuccessfulOffset}");
                            throw; // Re-throw to fail the job
                        }
                        else
                        {
                            // Data errors (constraints, invalid data) - skip batch and continue
                            await LogAsync(job.Id, DataCopyLogLevel.Error, $"Data error at offset {offset}: {batchEx.Message}");
                            await LogAsync(job.Id, DataCopyLogLevel.Warning, $"Skipping batch of {actualBatchSize} records and continuing...");
                            
                            job.FailedRecords += actualBatchSize;
                            await _jobRepo.UpdateJobAsync(job);
                            
                            offset += batchSize; // Skip failed batch and continue
                        }
                    }
                }

                // Mark as completed
                job.Status = DataCopyJobStatus.Completed;
                job.EndTime = DateTime.Now;
                job.Duration = (int)(job.EndTime.Value - job.StartTime.Value).TotalSeconds;
                job.ProgressPercentage = 100;
                job.CanResume = false; // Job completed, no need to resume
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(job.Id, DataCopyLogLevel.Info, $"Copy completed. Processed: {processedRecords}, Failed: {job.FailedRecords}");
            }
            catch (Exception ex)
            {
                job.Status = DataCopyJobStatus.Failed;
                job.EndTime = DateTime.Now;
                job.Duration = job.StartTime.HasValue ? (int)(job.EndTime.Value - job.StartTime.Value).TotalSeconds : 0;
                job.ErrorMessage = ex.Message;
                job.CanResume = true; // Allow resume on failure
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(job.Id, DataCopyLogLevel.Error, $"Copy failed: {ex.Message}. Job can be resumed from offset {job.LastSuccessfulOffset}");
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
                job.CanResume = false;
                await _jobRepo.UpdateJobAsync(job);
                await LogAsync(jobId, DataCopyLogLevel.Warning, "Job cancelled by user");
            }
        }

        public async Task<(bool Success, string Message)> ResumeJobAsync(int jobId)
        {
            try
            {
                var job = await _jobRepo.GetJobByIdAsync(jobId);
                if (job == null)
                    return (false, "Job not found.");

                if (!job.CanResume)
                    return (false, "Job cannot be resumed. It may have completed or been cancelled.");

                if (job.Status == DataCopyJobStatus.Running)
                    return (false, "Job is already running.");

                var config = await _configRepo.GetConfigurationByIdAsync(job.ConfigId);
                if (config == null)
                    return (false, "Configuration not found.");

                if (!config.Enabled)
                    return (false, "Configuration is disabled.");

                // Check retry limit
                if (job.RetryCount >= config.MaxRetryAttempts)
                    return (false, $"Maximum retry attempts ({config.MaxRetryAttempts}) reached.");

                // Increment retry count
                job.RetryCount++;
                job.Status = DataCopyJobStatus.Pending;
                job.ErrorMessage = null;
                await _jobRepo.UpdateJobAsync(job);

                // Execute copy in background (fire and forget)
                _ = Task.Run(async () => await ExecuteCopyAsync(job, config));

                return (true, $"Job resumed successfully from offset {job.LastSuccessfulOffset}. Retry attempt #{job.RetryCount}");
            }
            catch (Exception ex)
            {
                return (false, $"Error resuming job: {ex.Message}");
            }
        }

        private async Task RetryBatchIndividuallyAsync(DataCopyJob job, string destConnString, string destTableName, List<dynamic> batch, int offset, string batchError)
        {
            await LogAsync(job.Id, DataCopyLogLevel.Info, $"Retrying {batch.Count} records individually...");
            
            int successCount = 0;
            int failCount = 0;

            foreach (var record in batch)
            {
                try
                {
                    await _execRepo.InsertBatchAsync(destConnString, destTableName, new[] { record });
                    successCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    // Save to dead letter queue
                    await SaveFailedRecordAsync(job.Id, record, ex.Message);
                    await LogAsync(job.Id, DataCopyLogLevel.Warning, $"Failed to insert individual record: {ex.Message}");
                }
            }

            job.ProcessedRecords += successCount;
            job.FailedRecords += failCount;
            await _jobRepo.UpdateJobAsync(job);
            
            await LogAsync(job.Id, DataCopyLogLevel.Info, $"Individual retry completed. Success: {successCount}, Failed: {failCount}");
        }

        private bool IsTransientError(Exception ex)
        {
            // Check for transient/infrastructure errors that should cause job failure and resume
            var errorMessage = ex.Message.ToLower();
            var exceptionType = ex.GetType().Name.ToLower();
            
            // Connection errors
            if (errorMessage.Contains("connection") || 
                errorMessage.Contains("network") ||
                errorMessage.Contains("timeout") ||
                errorMessage.Contains("cannot open database") ||
                errorMessage.Contains("server") ||
                exceptionType.Contains("sqlexception") && errorMessage.Contains("transport-level"))
            {
                return true;
            }
            
            // Deadlock and transaction errors
            if (errorMessage.Contains("deadlock") ||
                errorMessage.Contains("transaction") && errorMessage.Contains("aborted"))
            {
                return true;
            }
            
            // Timeout errors
            if (errorMessage.Contains("timeout") || exceptionType.Contains("timeoutexception"))
            {
                return true;
            }
            
            // Data errors (constraints, invalid data) - NOT transient
            // These should skip the batch and continue
            return false;
        }

        private async Task SaveFailedRecordAsync(int jobId, dynamic record, string errorMessage)
        {
            try
            {
                var recordData = System.Text.Json.JsonSerializer.Serialize(record);
                var failedRecord = new DataCopyFailedRecord
                {
                    JobId = jobId,
                    RecordData = recordData,
                    ErrorMessage = errorMessage,
                    RetryCount = 0,
                    FailedDate = DateTime.Now,
                    Resolved = false
                };

                _dbContext.DataCopyFailedRecords.Add(failedRecord);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await LogAsync(jobId, DataCopyLogLevel.Error, $"Failed to save failed record to dead letter queue: {ex.Message}");
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
