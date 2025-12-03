using System.Collections.Generic;

namespace DataSync.Core.Models
{
    public class ExportDataResult
    {
        public IEnumerable<dynamic> Data { get; set; }
        public long LogId { get; set; }
        public string DbName { get; set; }
        public string TableName { get; set; }
    }
}
