using System;
using InventarioApp.Models;

namespace InventarioApp.Services
{
    public class AuditService
    {
        private DatabaseService? _db;

        // Permite inyectar la base de datos de manera diferida para evitar dependencias circulares en el contenedor DI
        public void Configure(DatabaseService db)
        {
            _db = db;
        }

        public void LogSecurityEvent(string userId, string username, string eventType, string description)
        {
            var log = new AuditLog
            {
                UserId = string.IsNullOrEmpty(userId) ? "UNKNOWN" : userId,
                Username = string.IsNullOrEmpty(username) ? "System/Anonymous" : username,
                EventTime = DateTime.UtcNow,
                EventType = eventType,
                Description = description
            };

            // Guarda localmente
            if (_db != null)
            {
                try
                {
                    _db.SaveAuditLog(log);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallo al escribir en la bitácora de auditoría: {ex.Message}");
                }
            }
        }
    }
}
