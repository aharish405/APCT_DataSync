using System;

namespace DataSync.Core.Models.DataCopy
{
    public class DataCopyFailedRecord
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public DataCopyJob Job { get; set; }
        public string RecordData { get; set; } // JSON serialized record
        public string ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public DateTime FailedDate { get; set; } = DateTime.Now;
        public DateTime? LastRetryDate { get; set; }
        public bool Resolved { get; set; }
    }
}
