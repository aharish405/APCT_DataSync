using System.Collections.Generic;
using System.Threading.Tasks;
using DataSync.Core.Models;

namespace DataSync.Core.Interfaces
{
    public interface IConfigurationRepository
    {
        Task<ExportConfiguration> GetConfigurationAsync(string appId);
        Task<ExportConfiguration> GetConfigurationAsync(string appName, string dbName, string tableName);
        Task<IEnumerable<ConfigDetailsResponse>> GetAllExportTablesAsync();
        Task<PagedResult<ExportConfiguration>> GetAllConfigurationsAsync(int pageNumber, int pageSize, string appName = null);
        Task<int> AddConfigurationAsync(ExportConfiguration config);
        Task UpdateConfigurationAsync(ExportConfiguration config);
        Task DeleteConfigurationAsync(int id);
        Task DeleteConfigurationsAsync(IEnumerable<int> ids);
        Task AddConfigurationsAsync(IEnumerable<ExportConfiguration> configs);
    }
}
