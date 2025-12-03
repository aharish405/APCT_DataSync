using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataSync.Core.Interfaces;

namespace DataSync.Services
{
    public class ExportService : IExportService
    {
        private readonly IConfigurationRepository _configRepo;
        private readonly IExportLogRepository _logRepo;
        private readonly IDataExportRepository _dataRepo;

        public ExportService(IConfigurationRepository configRepo, IExportLogRepository logRepo, IDataExportRepository dataRepo)
        {
            _configRepo = configRepo;
            _logRepo = logRepo;
            _dataRepo = dataRepo;
        }

        public async Task<DataSync.Core.Models.ExportDataResult> ExportDataAsync(string appName, string dbName, string tableName, DateTime fromDate, DateTime toDate)
        {
            var config = await _configRepo.GetConfigurationAsync(appName, dbName, tableName);
            if (config == null || !config.Enabled)
            {
                throw new Exception($"Configuration not found or disabled for App: {appName}, DB: {dbName}, Table: {tableName}");
            }

            var logId = await _logRepo.LogStartAsync(config.AppId, config.TableName, fromDate, toDate);

            try
            {
                var data = await _dataRepo.GetDataAsync(config, fromDate, toDate);
                return new DataSync.Core.Models.ExportDataResult
                {
                    Data = data,
                    LogId = logId,
                    DbName = config.DbName,
                    TableName = config.TableName
                };
            }
            catch (Exception ex)
            {
                await _logRepo.LogEndAsync(logId, 0, "Failed", ex.Message);
                throw;
            }
        }

        public async Task CompleteLogAsync(long logId, int recordsCount, string status, string errorMessage = null)
        {
            await _logRepo.LogEndAsync(logId, recordsCount, status, errorMessage);
        }
    }
}
