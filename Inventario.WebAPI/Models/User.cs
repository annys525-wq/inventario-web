using System;
using System.Collections.Generic;

namespace Inventario.WebAPI.Models
{
    public enum UserRole
    {
        Administrador,
        Vendedor,
        Bodega
    }

    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Devuelve los permisos asociados al rol del usuario
        public List<string> Permissions => GetPermissionsForRole(Role);

        public static List<string> GetPermissionsForRole(UserRole role)
        {
            return role switch
            {
                UserRole.Administrador => new List<string>
                {
                    "USER_CRUD",
                    "VIEW_AUDIT_LOGS",
                    "CRM_WRITE",
                    "CRM_READ",
                    "STOCK_READ",
                    "STOCK_WRITE",
                    "SALES_WRITE",
                    "PURCHASES_WRITE"
                },
                UserRole.Vendedor => new List<string>
                {
                    "CRM_READ",
                    "CRM_WRITE",
                    "STOCK_READ",
                    "SALES_WRITE"
                },
                UserRole.Bodega => new List<string>
                {
                    "STOCK_READ",
                    "STOCK_WRITE",
                    "PURCHASES_WRITE"
                },
                _ => new List<string>()
            };
        }

        public bool HasPermission(string permission)
        {
            return Permissions.Contains(permission);
        }
    }
}
