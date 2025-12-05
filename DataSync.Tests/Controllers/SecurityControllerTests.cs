using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using DataSync.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DataSync.Tests.Controllers
{
    public class SecurityControllerTests
    {
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly SecurityController _controller;

        public SecurityControllerTests()
        {
            _mockAuthService = new Mock<IAuthService>();
            _controller = new SecurityController(_mockAuthService.Object);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithClients()
        {
            // Arrange
            var clients = new List<ApiClient> { new ApiClient { ClientName = "Client1" } };
            _mockAuthService.Setup(s => s.GetAllClientsAsync()).ReturnsAsync(clients);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<ApiClient>>().Subject;
            model.Should().HaveCount(1);
        }

        [Fact]
        public async Task Create_Post_ShouldReturnViewWithSecret_WhenValid()
        {
            // Arrange
            var clientName = "NewClient";
            var client = new ApiClient { ClientName = clientName };
            var secret = "secret123";
            _mockAuthService.Setup(s => s.CreateClientAsync(clientName))
                .ReturnsAsync((client, secret));

            // Act
            var result = await _controller.Create(clientName);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("ShowSecret");
            viewResult.ViewData["PlainSecret"].Should().Be(secret);
        }
    }
}
