using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using InventarioApp.Models;

namespace InventarioApp.Services
{
    public class AuthService
    {
        private readonly DatabaseService _db;
        private readonly AuditService _audit;
        private const string SecretKey = "EnterpriseCRMSecretKey2026SuperSecureAndLongEnoughToAvoidSignatureErrors";
        private const string Issuer = "InventarioAppServer";
        private const string Audience = "InventarioAppClient";

        public User? CurrentUser { get; private set; }
        public string? CurrentJwtToken { get; private set; }

        public AuthService(DatabaseService db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        // Hashea una contraseña usando SHA256
        public static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        // Intenta iniciar sesión con JWT
        public bool Login(string username, string password)
        {
            User? user = _db.GetUserByUsername(username);

            if (user == null)
            {
                _audit.LogSecurityEvent("u0", username, "Login_Failed", $"Intento de acceso fallido: El usuario '{username}' no existe.");
                return false;
            }

            if (!user.IsActive)
            {
                _audit.LogSecurityEvent(user.Id, user.Username, "Login_Failed", $"Intento de acceso fallido: Cuenta desactivada para '{username}'.");
                return false;
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _audit.LogSecurityEvent(user.Id, user.Username, "Login_Failed", $"Intento de acceso fallido: Contraseña incorrecta para '{username}'.");
                return false;
            }

            CurrentUser = user;
            CurrentJwtToken = GenerateJwtToken(user);

            _audit.LogSecurityEvent(user.Id, user.Username, "Login_Success", $"Sesión iniciada con éxito. Token JWT generado.");
            return true;
        }

        public void Logout()
        {
            if (CurrentUser != null)
            {
                _audit.LogSecurityEvent(CurrentUser.Id, CurrentUser.Username, "Logout", "El usuario cerró su sesión activa.");
            }
            CurrentUser = null;
            CurrentJwtToken = null;
        }

        // Genera el JWT Token firmado
        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("FullName", user.FullName),
                new Claim("Email", user.Email)
            };

            // Agregar los permisos como claims individuales
            foreach (var perm in user.Permissions)
            {
                claims.Add(new Claim("Permission", perm));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8), // Expira en 8 horas de jornada laboral
                Issuer = Issuer,
                Audience = Audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // Valida el JWT y extrae los claims para reconstruir la sesión
        public bool ValidateAndSetSessionFromJwt(string jwtToken)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                ClaimsPrincipal principal = tokenHandler.ValidateToken(jwtToken, validationParameters, out SecurityToken validatedToken);

                // Obtener datos básicos
                string? id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                string? username = principal.FindFirst(ClaimTypes.Name)?.Value;
                string? roleStr = principal.FindFirst(ClaimTypes.Role)?.Value;
                string? fullName = principal.FindFirst("FullName")?.Value;
                string? email = principal.FindFirst("Email")?.Value;

                if (id != null && username != null && roleStr != null && fullName != null && email != null)
                {
                    if (Enum.TryParse<UserRole>(roleStr, out UserRole role))
                    {
                        CurrentUser = new User
                        {
                            Id = id,
                            Username = username,
                            FullName = fullName,
                            Email = email,
                            Role = role
                        };
                        CurrentJwtToken = jwtToken;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al validar token JWT: {ex.Message}");
            }
            return false;
        }

        // Valida si el usuario en sesión tiene el permiso específico
        public bool CheckAuthorize(string permission)
        {
            if (CurrentUser == null) return false;
            return CurrentUser.HasPermission(permission);
        }
    }
}
