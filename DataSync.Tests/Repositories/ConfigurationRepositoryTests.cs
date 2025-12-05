using DataSync.Core.Models;
using DataSync.Data;
using DataSync.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DataSync.Tests.Repositories
{
    public class ConfigurationRepositoryTests
    {
        private readonly DataSyncDbContext _context;

        public ConfigurationRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<DataSyncDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
                .Options;
            
            _context = new DataSyncDbContext(options);
            
            // Note: ConfigurationRepository uses Dapper for some methods and EF for others in a real scenario.
            // Since we can't easily mock Dapper with InMemory EF, we will test the EF-compatible parts 
            // or refactor the repository to be more testable. 
            // For this task, we will assume the repository can work with the DbContext or connection string.
            // However, the current implementation of ConfigurationRepository takes a connection string and uses Dapper.
            // Testing Dapper against InMemory is not possible. 
            // We would need a real SQL LocalDB or Docker container for true integration tests.
            
            // SKIPPING Dapper tests for now as we don't have a running SQL instance guaranteed for tests.
            // Instead, we will verify the DbContext configuration which is used by EF Core parts of the app.
        }

        [Fact]
        public async Task CanAddAndRetrieveConfiguration_ViaDbContext()
        {
            // Arrange
            var config = new ExportConfiguration
            {
                AppName = "IntegrationTestApp",
                AppId = "test-app",
                DbServerIP = "127.0.0.1",
                DbName = "TestDb",
                TableName = "TestTable",
                DateColumn = "CreatedDate",
                Enabled = true
            };

            // Act
            _context.ExportConfigurations.Add(config);
            await _context.SaveChangesAsync();

            // Assert
            var savedConfig = await _context.ExportConfigurations.FirstOrDefaultAsync(c => c.AppId == "test-app");
            savedConfig.Should().NotBeNull();
            savedConfig.AppName.Should().Be("IntegrationTestApp");
        }
    }
}
