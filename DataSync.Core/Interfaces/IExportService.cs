using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataSync.Core.Interfaces
{
    public interface IExportService
    {
        Task<DataSync.Core.Models.ExportDataResult> ExportDataAsync(string appName, string dbName, string tableName, DateTime fromDate, DateTime toDate);
        Task CompleteLogAsync(long logId, int recordsCount, string status, string errorMessage = null);
    }
}
