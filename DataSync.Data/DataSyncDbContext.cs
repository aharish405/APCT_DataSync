using DataSync.Core.Models;
using DataSync.Core.Models.DataCopy;
using Microsoft.EntityFrameworkCore;

namespace DataSync.Data
{
    public class DataSyncDbContext : DbContext
    {
        public DataSyncDbContext(DbContextOptions<DataSyncDbContext> options) : base(options)
        {
        }

        public DbSet<ExportConfiguration> ExportConfigurations { get; set; }
        public DbSet<ExportLog> ExportLogs { get; set; }
        
        // DataCopy DbSets
        public DbSet<DataCopyConfiguration> DataCopyConfigurations { get; set; }
        public DbSet<DataCopyJob> DataCopyJobs { get; set; }
        public DbSet<DataCopyJobLog> DataCopyJobLogs { get; set; }
        public DbSet<DataCopyFailedRecord> DataCopyFailedRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Export Configuration
            modelBuilder.Entity<ExportLog>(entity =>
            {
                entity.ToTable("DataSync_ExportLogs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AppId).IsRequired();
                entity.Property(e => e.TableName).HasMaxLength(100);
            });

            modelBuilder.Entity<ExportConfiguration>(entity =>
            {
                entity.ToTable("DataSync_ExportConfigurations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AppId).IsRequired();
            });
            
            // DataCopy Module Configurations
            modelBuilder.Entity<DataCopyConfiguration>(entity =>
            {
                entity.ToTable("DataCopy_Configurations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ConfigName).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.ConfigName).IsUnique();
            });
            
            modelBuilder.Entity<DataCopyJob>(entity =>
            {
                entity.ToTable("DataCopy_Jobs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.TriggerType).HasConversion<string>().HasMaxLength(20);
                entity.HasOne(e => e.Configuration)
                    .WithMany()
                    .HasForeignKey(e => e.ConfigId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            modelBuilder.Entity<DataCopyJobLog>(entity =>
            {
                entity.ToTable("DataCopy_JobLogs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LogLevel).HasConversion<string>().HasMaxLength(20);
            });
            
            modelBuilder.Entity<DataCopyFailedRecord>(entity =>
            {
                entity.ToTable("DataCopy_FailedRecords");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Job)
                    .WithMany()
                    .HasForeignKey(e => e.JobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
