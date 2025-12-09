using System.Collections.Generic;
using System.Threading.Tasks;
using DataSync.Core.Models;
using DataSync.Core.Models.DataCopy;

namespace DataSync.Core.Interfaces
{
    public interface IDataCopyConfigRepository
    {
        Task<DataCopyConfiguration> GetConfigurationByIdAsync(int id);
        Task<IEnumerable<DataCopyConfiguration>> GetAllConfigurationsAsync();
        Task<PagedResult<DataCopyConfiguration>> GetAllConfigurationsPagedAsync(int pageNumber, int pageSize, string configName = null);
        Task<int> AddConfigurationAsync(DataCopyConfiguration config);
        Task UpdateConfigurationAsync(DataCopyConfiguration config);
        Task DeleteConfigurationAsync(int id);
        Task<(bool Success, string Message)> ValidateConfigurationAsync(DataCopyConfiguration config);
    }
}
