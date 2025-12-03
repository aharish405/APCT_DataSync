using System;

namespace DataSync.Core.Models
{
    public class ApiClient
    {
        public int Id { get; set; }
        public string ClientId { get; set; }
        public string ClientSecretHash { get; set; }
        public string ClientName { get; set; }
        public bool IsActive { get; set; }
        public DateTime Created { get; set; }
    }
}
