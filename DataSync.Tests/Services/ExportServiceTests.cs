using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using DataSync.Services;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DataSync.Tests.Services
{
    public class ExportServiceTests
    {
        private readonly Mock<IConfigurationRepository> _mockConfigRepo;
        private readonly Mock<IDataExportRepository> _mockDataRepo;
        private readonly Mock<IExportLogRepository> _mockLogRepo;
        private readonly ExportService _exportService;

        public ExportServiceTests()
        {
            _mockConfigRepo = new Mock<IConfigurationRepository>();
            _mockDataRepo = new Mock<IDataExportRepository>();
            _mockLogRepo = new Mock<IExportLogRepository>();
            _exportService = new ExportService(_mockConfigRepo.Object, _mockLogRepo.Object, _mockDataRepo.Object);
        }

        [Fact]
        public async Task ExportDataAsync_ShouldReturnData_WhenConfigExists()
        {
            // Arrange
            var appName = "TestApp";
            var dbName = "TestDb";
            var tableName = "TestTable";
            var fromDate = DateTime.Now.AddDays(-1);
            var toDate = DateTime.Now;

            var config = new ExportConfiguration
            {
                Id = 1,
                AppName = appName,
                DbName = dbName,
                TableName = tableName,
                Enabled = true
            };

            var expectedData = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "Id", 1 }, { "Name", "Test" } }
            };

            _mockConfigRepo.Setup(r => r.GetConfigurationAsync(appName, dbName, tableName))
                .ReturnsAsync(config);

            _mockDataRepo.Setup(r => r.GetDataAsync(config, fromDate, toDate))
                .ReturnsAsync(expectedData);

            // Act
            var result = await _exportService.ExportDataAsync(appName, dbName, tableName, fromDate, toDate);

            // Assert
            result.Data.Should().BeEquivalentTo(expectedData);
            _mockLogRepo.Verify(r => r.LogStartAsync(config.AppId, config.TableName, fromDate, toDate), Times.Once);
        }

        [Fact]
        public async Task ExportDataAsync_ShouldThrowException_WhenConfigNotFound()
        {
            // Arrange
            var appName = "UnknownApp";
            
            _mockConfigRepo.Setup(r => r.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((ExportConfiguration)null);

            // Act
            Func<Task> act = async () => await _exportService.ExportDataAsync(appName, "db", "table", DateTime.MinValue, DateTime.MinValue);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Configuration not found*");
        }

        [Fact]
        public async Task ExportDataAsync_ShouldThrowException_WhenConfigIsDisabled()
        {
            // Arrange
            var config = new ExportConfiguration { AppName = "App", Enabled = false };
            _mockConfigRepo.Setup(r => r.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(config);

            // Act
            Func<Task> act = async () => await _exportService.ExportDataAsync("App", "db", "table", DateTime.Now, DateTime.Now);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Configuration not found or disabled*");
        }

        [Fact]
        public async Task ExportDataAsync_ShouldLogFailure_WhenRepositoryThrows()
        {
            // Arrange
            var config = new ExportConfiguration { AppName = "App", Enabled = true, AppId = "app-id", TableName = "table" };
            _mockConfigRepo.Setup(r => r.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(config);
            
            var logId = 123L;
            _mockLogRepo.Setup(r => r.LogStartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(logId);

            _mockDataRepo.Setup(r => r.GetDataAsync(It.IsAny<ExportConfiguration>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("DB Error"));

            // Act
            Func<Task> act = async () => await _exportService.ExportDataAsync("App", "db", "table", DateTime.Now, DateTime.Now);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
            _mockLogRepo.Verify(r => r.LogEndAsync(logId, 0, "Failed", "DB Error"), Times.Once);
        }

        [Fact]
        public async Task ExportDataAsync_ShouldReturnEmptyList_WhenNoDataFound()
        {
            // Arrange
            var config = new ExportConfiguration { AppName = "App", Enabled = true };
            _mockConfigRepo.Setup(r => r.GetConfigurationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(config);
            _mockDataRepo.Setup(r => r.GetDataAsync(It.IsAny<ExportConfiguration>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Dictionary<string, object>>());

            // Act
            var result = await _exportService.ExportDataAsync("App", "db", "table", DateTime.Now, DateTime.Now);

            // Assert
            result.Data.Should().BeEmpty();
        }
    }
}
