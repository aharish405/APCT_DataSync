using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataSync.Core.Interfaces
{
    public interface IDataCopyExecutionRepository
    {
        Task<int> GetSourceRecordCountAsync(string connectionString, string tableName, string customQuery);
        Task<IEnumerable<dynamic>> GetSourceDataAsync(string connectionString, string tableName, string customQuery, int offset, int batchSize);
        Task<int> InsertBatchAsync(string connectionString, string tableName, IEnumerable<dynamic> data);
        Task TruncateDestinationAsync(string connectionString, string tableName);
        Task<(bool Success, string ErrorMessage)> TestConnectionAsync(string connectionString);
        Task<bool> TableExistsAsync(string connectionString, string tableName);
    }
}
