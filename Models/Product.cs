using System;

namespace InventarioApp.Models
{
    public class Product
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal Price { get; set; }
        public int WarehouseMain { get; set; }
        public int WarehouseSecondary { get; set; }
        public int MinimumStock { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "System";

        public int TotalStock => WarehouseMain + WarehouseSecondary;
        public bool IsLowStock => TotalStock <= MinimumStock;
    }
}
