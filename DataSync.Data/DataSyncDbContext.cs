using DataSync.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DataSync.Data
{
    public class DataSyncDbContext : DbContext
    {
        public DataSyncDbContext(DbContextOptions<DataSyncDbContext> options) : base(options)
        {
        }

        public DbSet<ExportLog> ExportLogs { get; set; }
        public DbSet<ExportConfiguration> ExportConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
        }
    }
}
