using System;
using Google.Cloud.Firestore;

namespace Inventario.WebAPI.Models
{
    [FirestoreData]
    public class Customer
    {
        [FirestoreProperty]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [FirestoreProperty]
        public string FullName { get; set; } = string.Empty;
        [FirestoreProperty]
        public string TaxId { get; set; } = string.Empty;
        [FirestoreProperty]
        public string Email { get; set; } = string.Empty;
        [FirestoreProperty]
        public string Phone { get; set; } = string.Empty;
        [FirestoreProperty]
        public string PipelineStage { get; set; } = "Prospecto";
        [FirestoreProperty]
        public double CreditLimit { get; set; }
        [FirestoreProperty]
        public double OutstandingBalance { get; set; }
        [FirestoreProperty]
        public bool IsActive { get; set; } = true;
        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [FirestoreProperty]
        public string UpdatedBy { get; set; } = "System";
    }
}
