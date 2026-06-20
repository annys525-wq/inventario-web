using System;
using Google.Cloud.Firestore;

namespace Inventario.WebAPI.Models
{
    [FirestoreData]
    public class Supplier
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
        public string Address { get; set; } = string.Empty;
        [FirestoreProperty]
        public string ContactPerson { get; set; } = string.Empty;
        [FirestoreProperty]
        public bool IsActive { get; set; } = true;
        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [FirestoreProperty]
        public string UpdatedBy { get; set; } = "System";
    }
}
