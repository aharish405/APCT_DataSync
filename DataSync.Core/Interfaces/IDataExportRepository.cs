using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataSync.Core.Models;

namespace DataSync.Core.Interfaces
{
    public interface IDataExportRepository
    {
        Task<IEnumerable<dynamic>> GetDataAsync(ExportConfiguration config, DateTime fromDate, DateTime toDate);
        Task<(bool Success, string Message, int Count)> ValidateQueryAsync(ExportConfiguration config, DateTime? testFromDate = null, DateTime? testToDate = null);
    }
}
