using System;

namespace DataSync.Core.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public string AppId { get; set; }
        public string Token { get; set; }
        public DateTime Expires { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Revoked { get; set; }
        public bool IsActive => Revoked == null && DateTime.UtcNow <= Expires;
    }
}
