using System;

namespace DataSync.Core.Models
{
    public class ExportConfiguration
    {
        public int Id { get; set; }
        public string AppId { get; set; }
        public string AppName { get; set; }
        public string DbServerIP { get; set; }
        public string DbName { get; set; }
        public string TableName { get; set; }
        public string DateColumn { get; set; }
        public string CustomQuery { get; set; }
        public bool Enabled { get; set; }
    }
}
