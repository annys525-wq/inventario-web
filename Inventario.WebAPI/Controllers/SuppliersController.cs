using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Inventario.WebAPI.Services;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Controllers
{
    [ApiController]
    [Route("api/suppliers")]
    public class SuppliersController : ControllerBase
    {
        private readonly FirestoreService _db;

        public SuppliersController(FirestoreService db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetSuppliers()
        {
            var suppliers = await _db.GetSuppliersAsync();
            return Ok(suppliers);
        }

        [HttpPost]
        public async Task<IActionResult> SaveSupplier([FromBody] Supplier supplier)
        {
            if (supplier == null) return BadRequest();

            if (string.IsNullOrEmpty(supplier.Id))
            {
                supplier.Id = Guid.NewGuid().ToString();
            }
            supplier.UpdatedAt = DateTime.UtcNow;

            await _db.SaveSupplierAsync(supplier);

            var log = new AuditLog
            {
                UserId = "System",
                Username = supplier.UpdatedBy,
                EventType = "Supplier_Saved",
                Description = $"Proveedor '{supplier.FullName}' (NIT/ID: {supplier.TaxId}) guardado/actualizado mediante la Web API."
            };
            await _db.SaveAuditLogAsync(log);

            return Ok(supplier);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(string id, [FromQuery] string updatedBy = "System")
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            await _db.DeleteSupplierAsync(id);

            var log = new AuditLog
            {
                UserId = "System",
                Username = updatedBy,
                EventType = "Supplier_Deleted",
                Description = $"Proveedor con ID {id} eliminado mediante la Web API."
            };
            await _db.SaveAuditLogAsync(log);

            return Ok(new { message = "Proveedor eliminado correctamente." });
        }
    }
}
