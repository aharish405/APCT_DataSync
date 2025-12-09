using System;

namespace DataSync.Core.Models.DataCopy
{
    public class DataCopyJob
    {
        public int Id { get; set; }
        public int ConfigId { get; set; }
        
        // Job Status
        public DataCopyJobStatus Status { get; set; }
        public DataCopyTriggerType TriggerType { get; set; }
        
        // Progress Tracking
        public int? TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int FailedRecords { get; set; }
        public decimal ProgressPercentage { get; set; }
        
        // Timing
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Duration { get; set; } // Seconds
        
        // Error Handling
        public string ErrorMessage { get; set; }
        
        public DateTime CreatedDate { get; set; }
        
        // Navigation property
        public DataCopyConfiguration Configuration { get; set; }
    }
}
