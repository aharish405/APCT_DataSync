using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using Microsoft.IdentityModel.Tokens;

namespace DataSync.Services
{
    public class AuthService : IAuthService
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly IAuthRepository _authRepo;

        public AuthService(string secretKey, string issuer, string audience, IAuthRepository authRepo)
        {
            _secretKey = secretKey;
            _issuer = issuer;
            _audience = audience;
            _authRepo = authRepo;
        }

        public async Task<TokenResponse> GenerateTokenAsync(string appId)
        {
            return await CreateTokenResponse(appId);
        }

        public async Task<TokenResponse> RefreshTokenAsync(string token, string clientId)
        {
            var refreshToken = await _authRepo.GetRefreshTokenAsync(token);

            if (refreshToken == null || !refreshToken.IsActive || refreshToken.AppId != clientId)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }

            // Revoke old token (Rotation)
            await _authRepo.RevokeRefreshTokenAsync(token);

            return await CreateTokenResponse(clientId);
        }

        private async Task<TokenResponse> CreateTokenResponse(string appId)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, appId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(_issuer,
              _audience,
              claims,
              expires: DateTime.UtcNow.AddMinutes(60),
              signingCredentials: credentials);

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            var refreshToken = GenerateRefreshTokenString();

            var refreshTokenEntity = new RefreshToken
            {
                AppId = appId,
                Token = refreshToken,
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow
            };

            await _authRepo.SaveRefreshTokenAsync(refreshTokenEntity);

            return new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 3600
            };
        }

        private string GenerateRefreshTokenString()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        public async Task<ApiClient> ValidateClientAsync(string clientId, string clientSecret)
        {
            var client = await _authRepo.GetClientAsync(clientId);
            if (client == null)
            {
                return null;
            }

            var secretHash = ComputeSha256Hash(clientSecret);
            if (client.ClientSecretHash != secretHash)
            {
                return null;
            }

            return client;
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (var sha256Hash = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public bool ValidateAppId(string appId)
        {
            return !string.IsNullOrEmpty(appId);
        }

        public async Task<System.Collections.Generic.IEnumerable<ApiClient>> GetAllClientsAsync()
        {
            return await _authRepo.GetAllClientsAsync();
        }

        public async Task<(ApiClient client, string plainSecret)> CreateClientAsync(string clientName)
        {
            var clientId = Guid.NewGuid().ToString("N");
            var plainSecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var secretHash = ComputeSha256Hash(plainSecret);

            var client = new ApiClient
            {
                ClientId = clientId,
                ClientSecretHash = secretHash,
                ClientName = clientName,
                IsActive = true,
                Created = DateTime.UtcNow
            };

            var id = await _authRepo.CreateClientAsync(client);
            client.Id = id;

            return (client, plainSecret);
        }

        public async Task DeleteClientAsync(int id)
        {
            await _authRepo.DeleteClientAsync(id);
        }

        public async Task<System.Collections.Generic.IEnumerable<RefreshToken>> GetAllRefreshTokensAsync()
        {
            return await _authRepo.GetAllRefreshTokensAsync();
        }

        public async Task RevokeRefreshTokenByIdAsync(int id)
        {
            await _authRepo.RevokeRefreshTokenByIdAsync(id);
        }
    }
}
