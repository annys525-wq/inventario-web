using System;
using System.Collections.Generic;

namespace InventarioApp.Models
{
    public class Purchase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PurchaseNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string SupplierId { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Warehouse { get; set; } = "Main"; // Main or Secondary
        public string Observations { get; set; } = string.Empty;
        public string RegisteredBy { get; set; } = "System";
        public List<PurchaseLine> Lines { get; set; } = new();
    }

    public class PurchaseLine
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PurchaseId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string EAN { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public int Qty { get; set; }
        public decimal Subtotal => Cost * Qty;
    }
}
