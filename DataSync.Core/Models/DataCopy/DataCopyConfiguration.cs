using System;
using System.ComponentModel.DataAnnotations;

namespace DataSync.Core.Models.DataCopy
{
    public class DataCopyConfiguration
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Configuration Name")]
        public string ConfigName { get; set; }

        [Required]
        [Display(Name = "Source Server")]
        public string SourceDbServerIP { get; set; }

        [Required]
        [Display(Name = "Source Database")]
        public string SourceDbName { get; set; }

        [Required]
        [Display(Name = "Source Table")]
        public string SourceTableName { get; set; }

        [Display(Name = "Custom Query (WHERE clause)")]
        public string? SourceCustomQuery { get; set; }

        [Required]
        [Display(Name = "Destination Server")]
        public string DestDbServerIP { get; set; }

        [Required]
        [Display(Name = "Destination Database")]
        public string DestDbName { get; set; }

        [Required]
        [Display(Name = "Destination Table")]
        public string DestTableName { get; set; }

        [Display(Name = "Truncate Before Copy")]
        public bool TruncateBeforeCopy { get; set; }

        [Display(Name = "Batch Size")]
        public int BatchSize { get; set; } = 1000;

        [Display(Name = "Enable Scheduling")]
        public bool IsScheduled { get; set; }

        [Display(Name = "Cron Expression")]
        public string? ScheduleCron { get; set; }

        [Display(Name = "Enabled")]
        public bool Enabled { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;
    }
}
