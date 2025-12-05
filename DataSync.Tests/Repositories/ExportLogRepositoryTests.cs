using DataSync.Core.Models;
using DataSync.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DataSync.Tests.Repositories
{
    public class ExportLogRepositoryTests
    {
        private readonly DataSyncDbContext _context;

        public ExportLogRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<DataSyncDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DataSyncDbContext(options);
        }

        [Fact]
        public async Task CanAddAndRetrieveExportLog()
        {
            // Arrange
            var log = new ExportLog
            {
                AppId = "app-id",
                TableName = "table",
                RequestTimestamp = DateTime.UtcNow,
                FromDate = DateTime.UtcNow.AddDays(-1),
                ToDate = DateTime.UtcNow,
                Status = "Started"
            };

            // Act
            _context.ExportLogs.Add(log);
            await _context.SaveChangesAsync();

            // Assert
            var saved = await _context.ExportLogs.FirstOrDefaultAsync(l => l.AppId == "app-id");
            saved.Should().NotBeNull();
            saved.Status.Should().Be("Started");
        }
    }
}
