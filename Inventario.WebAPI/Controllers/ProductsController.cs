using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Inventario.WebAPI.Services;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly FirestoreService _db;

        public ProductsController(FirestoreService db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _db.GetProductsAsync();
            return Ok(products);
        }

        [HttpPost]
        public async Task<IActionResult> SaveProduct([FromBody] Product product)
        {
            if (product == null) return BadRequest();
            
            if (string.IsNullOrEmpty(product.Id))
            {
                product.Id = Guid.NewGuid().ToString();
            }
            product.UpdatedAt = DateTime.UtcNow;

            await _db.SaveProductAsync(product);
            
            var log = new AuditLog
            {
                UserId = "System",
                Username = product.UpdatedBy,
                EventType = "Product_Saved",
                Description = $"Producto '{product.Name}' (SKU: {product.SKU}) guardado/actualizado mediante la Web API."
            };
            await _db.SaveAuditLogAsync(log);

            return Ok(product);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(string id, [FromQuery] string updatedBy = "System")
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            await _db.DeleteProductAsync(id);

            var log = new AuditLog
            {
                UserId = "System",
                Username = updatedBy,
                EventType = "Product_Deleted",
                Description = $"Producto con ID {id} eliminado mediante la Web API."
            };
            await _db.SaveAuditLogAsync(log);

            return Ok(new { message = "Producto eliminado correctamente." });
        }
    }
}
