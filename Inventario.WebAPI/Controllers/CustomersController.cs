using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Inventario.WebAPI.Services;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomersController : ControllerBase
    {
        private readonly FirestoreService _db;

        public CustomersController(FirestoreService db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _db.GetCustomersAsync();
            return Ok(customers);
        }

        [HttpPost]
        public async Task<IActionResult> SaveCustomer([FromBody] Customer customer)
        {
            if (customer == null) return BadRequest();

            if (string.IsNullOrEmpty(customer.Id))
            {
                customer.Id = Guid.NewGuid().ToString();
            }
            customer.UpdatedAt = DateTime.UtcNow;

            await _db.SaveCustomerAsync(customer);

            var log = new AuditLog
            {
                UserId = "System",
                Username = customer.UpdatedBy,
                EventType = "Customer_Saved",
                Description = $"Cliente '{customer.FullName}' (NIT/ID: {customer.TaxId}) guardado/actualizado mediante la Web API."
            };
            await _db.SaveAuditLogAsync(log);

            return Ok(customer);
        }
    }
}
