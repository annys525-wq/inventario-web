using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Inventario.WebAPI.Services;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly FirestoreService _db;

        public AuthController(AuthService authService, FirestoreService db)
        {
            _authService = authService;
            _db = db;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Usuario y contraseña son requeridos." });
            }

            var (success, token, user) = await _authService.LoginAsync(request.Username, request.Password);

            if (!success || user == null)
            {
                var anonymousLog = new AuditLog
                {
                    UserId = "u0",
                    Username = request.Username,
                    EventType = "Login_Failed",
                    Description = $"Intento de acceso fallido web: Usuario o contraseña incorrectos."
                };
                await _db.SaveAuditLogAsync(anonymousLog);
                return Unauthorized(new { message = "Credenciales incorrectas o usuario inactivo." });
            }

            var log = new AuditLog
            {
                UserId = user.Id,
                Username = user.Username,
                EventType = "Login_Success",
                Description = $"Sesión iniciada con éxito en la web. Token JWT generado."
            };
            await _db.SaveAuditLogAsync(log);

            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.Username,
                    user.FullName,
                    user.Email,
                    user.Role,
                    RoleName = user.Role.ToString(),
                    user.Permissions
                }
            });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
