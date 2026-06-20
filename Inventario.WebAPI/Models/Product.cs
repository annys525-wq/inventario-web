using System;
using Google.Cloud.Firestore;

namespace Inventario.WebAPI.Models
{
    [FirestoreData]
    public class Product
    {
        [FirestoreProperty]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [FirestoreProperty]
        public string SKU { get; set; } = string.Empty;
        [FirestoreProperty]
        public string EAN { get; set; } = string.Empty;
        [FirestoreProperty]
        public string Name { get; set; } = string.Empty;
        [FirestoreProperty]
        public string Category { get; set; } = string.Empty;
        [FirestoreProperty]
        public double Cost { get; set; }
        [FirestoreProperty]
        public double Price { get; set; }
        [FirestoreProperty]
        public int WarehouseMain { get; set; }
        [FirestoreProperty]
        public int WarehouseSecondary { get; set; }
        [FirestoreProperty]
        public int MinimumStock { get; set; }
        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [FirestoreProperty]
        public string UpdatedBy { get; set; } = "System";

        public int TotalStock => WarehouseMain + WarehouseSecondary;
        public bool IsLowStock => TotalStock <= MinimumStock;
    }
}
