using System.Threading.Tasks;
using DataSync.Core.Models;

namespace DataSync.Core.Interfaces
{
    public interface IAuthService
    {
        Task<DataSync.Core.Models.TokenResponse> GenerateTokenAsync(string appId);
        Task<DataSync.Core.Models.TokenResponse> RefreshTokenAsync(string token, string clientId);
        Task<ApiClient> ValidateClientAsync(string clientId, string clientSecret);
        Task<System.Collections.Generic.IEnumerable<ApiClient>> GetAllClientsAsync();
        Task<(ApiClient client, string plainSecret)> CreateClientAsync(string clientName);
        Task DeleteClientAsync(int id);
        Task<System.Collections.Generic.IEnumerable<RefreshToken>> GetAllRefreshTokensAsync();
        Task RevokeRefreshTokenByIdAsync(int id);
    }
}
