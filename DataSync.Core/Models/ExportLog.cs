using System;

namespace DataSync.Core.Models
{
    public class ExportLog
    {
        public long Id { get; set; }
        public string AppId { get; set; }
        public string TableName { get; set; }
        public DateTime RequestTimestamp { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Status { get; set; }
        public int? RecordsCount { get; set; }
        public string ErrorMessage { get; set; }
    }
}
