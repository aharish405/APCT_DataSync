using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using DataSync.Data;
using DataSync.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DataSync.Tests.Controllers
{
    public class ConfigurationControllerTests
    {
        private readonly Mock<IConfigurationRepository> _mockRepo;
        private readonly Mock<IDataExportRepository> _mockDataExportRepo;
        private readonly DataSyncDbContext _dbContext;
        private readonly ConfigurationController _controller;

        public ConfigurationControllerTests()
        {
            _mockRepo = new Mock<IConfigurationRepository>();
            _mockDataExportRepo = new Mock<IDataExportRepository>();
            
            var options = new DbContextOptionsBuilder<DataSyncDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_ConfigController")
                .Options;
            _dbContext = new DataSyncDbContext(options);
            
            _controller = new ConfigurationController(_mockRepo.Object, _dbContext, _mockDataExportRepo.Object);
        }

        [Fact]
        public void Index_ShouldReturnViewWithConfigurations()
        {
            // Arrange
            _dbContext.Database.EnsureDeleted();
            _dbContext.Database.EnsureCreated();
            _dbContext.ExportConfigurations.Add(new ExportConfiguration { AppName = "App1", AppId = "1" });
            _dbContext.SaveChanges();

            // Act
            var result = _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<ExportConfiguration>>().Subject;
            model.Should().HaveCount(1);
        }

        [Fact]
        public async Task Create_Post_ShouldRedirect_WhenModelIsValid()
        {
            // Arrange
            var config = new ExportConfiguration 
            { 
                AppName = "NewApp", 
                AppId = "new-app",
                DbServerIP = "192.168.1.1",
                DbName = "TestDB",
                TableName = "TestTable",
                DateColumn = "CreatedDate",
                Enabled = true
            };
            
            _mockDataExportRepo.Setup(r => r.ValidateQueryAsync(It.IsAny<ExportConfiguration>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync((true, "Valid", 10));

            // Act
            var result = await _controller.Create(config);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            _mockRepo.Verify(r => r.AddConfigurationAsync(config), Times.Once);
        }

        [Fact]
        public async Task GetExportTables_ShouldReturnOk_WithList()
        {
            // Arrange
            var tables = new List<ConfigDetailsResponse> { new ConfigDetailsResponse { AppName = "App1" } };
            _mockRepo.Setup(r => r.GetAllExportTablesAsync()).ReturnsAsync(tables);

            // Act
            var result = await _controller.GetExportTables();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(tables);

        }

        [Fact]
        public async Task Create_Post_ShouldReturnView_WhenModelIsInvalid()
        {
            // Arrange
            _controller.ModelState.AddModelError("AppName", "Required");
            var config = new ExportConfiguration();

            // Act
            var result = await _controller.Create(config);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().Be(config);
            _mockRepo.Verify(r => r.AddConfigurationAsync(It.IsAny<ExportConfiguration>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_ShouldRedirect_WhenModelIsValid()
        {
            // Arrange
            var config = new ExportConfiguration 
            { 
                Id = 1, 
                AppName = "Updated",
                AppId = "updated-app",
                DbServerIP = "192.168.1.1",
                DbName = "TestDB",
                TableName = "TestTable",
                DateColumn = "CreatedDate",
                Enabled = true
            };
            
            _mockDataExportRepo.Setup(r => r.ValidateQueryAsync(It.IsAny<ExportConfiguration>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync((true, "Valid", 10));

            // Act
            var result = await _controller.Edit(config);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            _mockRepo.Verify(r => r.UpdateConfigurationAsync(config), Times.Once);
        }

        [Fact]
        public async Task Delete_ShouldRedirect_AndCallRepo()
        {
            // Arrange
            var id = 1;

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            _mockRepo.Verify(r => r.DeleteConfigurationAsync(id), Times.Once);
        }

    }
}
