using System.Collections.Generic;
using System.Threading.Tasks;
using DataSync.Core.Models.DataCopy;

namespace DataSync.Core.Interfaces
{
    public interface IDataCopyJobRepository
    {
        Task<int> CreateJobAsync(DataCopyJob job);
        Task UpdateJobAsync(DataCopyJob job);
        Task<DataCopyJob> GetJobByIdAsync(int id);
        Task<IEnumerable<DataCopyJob>> GetJobsByConfigIdAsync(int configId);
        Task<IEnumerable<DataCopyJob>> GetRecentJobsAsync(int count);
        Task<IEnumerable<DataCopyJob>> GetActiveJobsAsync();
        Task AddJobLogAsync(DataCopyJobLog log);
        Task<IEnumerable<DataCopyJobLog>> GetJobLogsAsync(int jobId);
    }
}
