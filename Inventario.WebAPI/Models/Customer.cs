using System;

namespace Inventario.WebAPI.Models
{
    public class Customer
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PipelineStage { get; set; } = "Prospecto";
        public decimal CreditLimit { get; set; }
        public decimal OutstandingBalance { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "System";
    }
}
