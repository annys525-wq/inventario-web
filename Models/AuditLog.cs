using System;

namespace InventarioApp.Models
{
    public class AuditLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
        public string EventType { get; set; } = string.Empty; // Login_Success, Login_Failed, Access_Denied, User_Created, etc.
        public string MachineName { get; set; } = Environment.MachineName;
        public string Description { get; set; } = string.Empty;
    }
}
