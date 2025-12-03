using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataSync.Core.Models;

namespace DataSync.Core.Interfaces
{
    public interface IDataExportRepository
    {
        Task<IEnumerable<dynamic>> GetDataAsync(ExportConfiguration config, DateTime fromDate, DateTime toDate);
    }
}
