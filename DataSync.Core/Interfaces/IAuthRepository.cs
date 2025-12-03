using System.Collections.Generic;
using System.Threading.Tasks;
using DataSync.Core.Models;

namespace DataSync.Core.Interfaces
{
    public interface IAuthRepository
    {
        Task SaveRefreshTokenAsync(RefreshToken token);
        Task<RefreshToken> GetRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token);
        Task<ApiClient> GetClientAsync(string clientId);
        Task<IEnumerable<ApiClient>> GetAllClientsAsync();
        Task<int> CreateClientAsync(ApiClient client);
        Task DeleteClientAsync(int id);
        Task<IEnumerable<RefreshToken>> GetAllRefreshTokensAsync();
        Task RevokeRefreshTokenByIdAsync(int id);
    }
}
