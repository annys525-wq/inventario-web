using System;

namespace InventarioApp.Models
{
    public class SyncItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TableName { get; set; } = string.Empty; // Users, Products, Customers, etc.
        public string RecordId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // INSERT, UPDATE, DELETE
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
