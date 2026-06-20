using System;
using Google.Cloud.Firestore;

namespace Inventario.WebAPI.Models
{
    [FirestoreData]
    public class AuditLog
    {
        [FirestoreProperty]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [FirestoreProperty]
        public string UserId { get; set; } = string.Empty;
        [FirestoreProperty]
        public string Username { get; set; } = string.Empty;
        [FirestoreProperty]
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
        [FirestoreProperty]
        public string EventType { get; set; } = string.Empty; // Login_Success, Login_Failed, Access_Denied, User_Created, etc.
        [FirestoreProperty]
        public string MachineName { get; set; } = Environment.MachineName;
        [FirestoreProperty]
        public string Description { get; set; } = string.Empty;
    }
}
