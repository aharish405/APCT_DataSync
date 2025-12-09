using System;

namespace DataSync.Core.Models.DataCopy
{
    public class DataCopyJobLog
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public DataCopyLogLevel LogLevel { get; set; }
        public string Message { get; set; }
        public DateTime LogTime { get; set; }
    }
}
