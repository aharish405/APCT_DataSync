using System.Threading.Tasks;
using DataSync.Core.Models.DataCopy;

namespace DataSync.Core.Interfaces
{
    public interface IDataCopyService
    {
        Task<(bool Success, string Message, int JobId)> ExecuteCopyJobAsync(int configId, DataCopyTriggerType triggerType);
        Task<DataCopyJob> GetJobStatusAsync(int jobId);
        Task<(bool Success, string Message)> ValidateConfigurationAsync(int configId);
        Task CancelJobAsync(int jobId);
    }
}
