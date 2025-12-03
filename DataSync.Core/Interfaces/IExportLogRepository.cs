using System.Threading.Tasks;
using DataSync.Core.Models;
using System.Collections.Generic;
using System;

namespace DataSync.Core.Interfaces
{
    public interface IExportLogRepository
    {
        Task<long> LogStartAsync(string appId, string tableName, DateTime fromDate, DateTime toDate);
        Task LogEndAsync(long logId, int recordsCount, string status, string errorMessage = null);
        Task<PagedResult<ExportLog>> GetLogsAsync(int pageNumber, int pageSize, string appId = null, string status = null);
    }
}
