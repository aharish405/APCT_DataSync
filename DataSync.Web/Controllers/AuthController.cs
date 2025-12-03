using System.Threading.Tasks;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataSync.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("token")]
        public async Task<IActionResult> GetToken([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ClientId) || string.IsNullOrEmpty(request.ClientSecret))
            {
                return BadRequest("ClientId and ClientSecret are required");
            }

            var client = await _authService.ValidateClientAsync(request.ClientId, request.ClientSecret);
            if (client == null)
            {
                return Unauthorized("Invalid client credentials");
            }

            var tokenResponse = await _authService.GenerateTokenAsync(request.ClientId);
            return Ok(tokenResponse);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken) || string.IsNullOrEmpty(request.ClientId))
            {
                return BadRequest("RefreshToken and ClientId are required");
            }

            try
            {
                var tokenResponse = await _authService.RefreshTokenAsync(request.RefreshToken, request.ClientId);
                return Ok(tokenResponse);
            }
            catch (System.Exception)
            {
                return Unauthorized("Invalid refresh token");
            }
        }
    }

    public class RefreshTokenRequest
    {
        public string ClientId { get; set; }
        public string RefreshToken { get; set; }
    }
}
