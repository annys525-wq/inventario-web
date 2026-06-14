using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Services
{
    public class AuthService
    {
        private readonly FirestoreService _db;
        private const string SecretKey = "EnterpriseCRMSecretKey2026SuperSecureAndLongEnoughToAvoidSignatureErrors";
        private const string Issuer = "InventarioAppServer";
        private const string Audience = "InventarioAppClient";

        public AuthService(FirestoreService db)
        {
            _db = db;
        }

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

        public async Task<(bool success, string? token, User? user)> LoginAsync(string username, string password)
        {
            User? user = await _db.GetUserByUsernameAsync(username);

            if (user == null || !user.IsActive)
            {
                return (false, null, null);
            }

            string hash = HashPassword(password);
            if (user.PasswordHash != hash)
            {
                return (false, null, null);
            }

            string token = GenerateJwtToken(user);
            return (true, token, user);
        }

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

            foreach (var perm in user.Permissions)
            {
                claims.Add(new Claim("Permission", perm));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                Issuer = Issuer,
                Audience = Audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
