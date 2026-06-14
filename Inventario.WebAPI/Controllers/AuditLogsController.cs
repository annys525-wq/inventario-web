using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Inventario.WebAPI.Services;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Controllers
{
    [ApiController]
    [Route("api/auditlogs")]
    public class AuditLogsController : ControllerBase
    {
        private readonly FirestoreService _db;

        public AuditLogsController(FirestoreService db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditLogs()
        {
            var logs = await _db.GetAuditLogsAsync();
            return Ok(logs);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAuditLog([FromBody] AuditLog log)
        {
            if (log == null) return BadRequest();
            await _db.SaveAuditLogAsync(log);
            return Ok(log);
        }
    }
}
