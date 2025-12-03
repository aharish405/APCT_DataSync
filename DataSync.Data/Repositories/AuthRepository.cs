using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;

namespace DataSync.Data.Repositories
{
    public class AuthRepository : IAuthRepository
    {
        private readonly string _connectionString;

        public AuthRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task SaveRefreshTokenAsync(RefreshToken token)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO DataSync_RefreshTokens (AppId, Token, Expires, Created, Revoked)
                    VALUES (@AppId, @Token, @Expires, @Created, @Revoked)";
                await connection.ExecuteAsync(sql, token);
            }
        }

        public async Task<RefreshToken> GetRefreshTokenAsync(string token)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_RefreshTokens WHERE Token = @Token";
                return await connection.QueryFirstOrDefaultAsync<RefreshToken>(sql, new { Token = token });
            }
        }

        public async Task RevokeRefreshTokenAsync(string token)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "UPDATE DataSync_RefreshTokens SET Revoked = GETDATE() WHERE Token = @Token";
                await connection.ExecuteAsync(sql, new { Token = token });
            }
        }

        public async Task<ApiClient> GetClientAsync(string clientId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_ApiClients WHERE ClientId = @ClientId AND IsActive = 1";
                return await connection.QuerySingleOrDefaultAsync<ApiClient>(sql, new { ClientId = clientId });
            }
        }

        public async Task<IEnumerable<ApiClient>> GetAllClientsAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_ApiClients WHERE IsActive = 1";
                return await connection.QueryAsync<ApiClient>(sql);
            }
        }

        public async Task<int> CreateClientAsync(ApiClient client)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = @"
                    INSERT INTO DataSync_ApiClients (ClientId, ClientSecretHash, ClientName, IsActive, Created)
                    VALUES (@ClientId, @ClientSecretHash, @ClientName, 1, GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() as int)";
                return await connection.QuerySingleAsync<int>(sql, client);
            }
        }

        public async Task DeleteClientAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "UPDATE DataSync_ApiClients SET IsActive = 0 WHERE Id = @Id";
                await connection.ExecuteAsync(sql, new { Id = id });
            }
        }

        public async Task<IEnumerable<RefreshToken>> GetAllRefreshTokensAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "SELECT * FROM DataSync_RefreshTokens ORDER BY Created DESC";
                return await connection.QueryAsync<RefreshToken>(sql);
            }
        }

        public async Task RevokeRefreshTokenByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var sql = "UPDATE DataSync_RefreshTokens SET Revoked = GETDATE() WHERE Id = @Id";
                await connection.ExecuteAsync(sql, new { Id = id });
            }
        }
    }
}
