using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Services
{
    public class FirestoreService
    {
        private readonly FirestoreDb _db;

        public FirestoreService(FirestoreDb db)
        {
            _db = db;
        }

        // ── Users ────────────────────────────────────────────────────────
        public async Task<List<User>> GetUsersAsync()
        {
            var snapshot = await _db.Collection("users").GetSnapshotAsync();
            var users = new List<User>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    users.Add(doc.ConvertTo<User>());
                }
            }
            return users;
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            var query = _db.Collection("users").WhereEqualTo("Username", username).Limit(1);
            var snapshot = await query.GetSnapshotAsync();
            if (snapshot.Documents.Count > 0)
            {
                return snapshot.Documents[0].ConvertTo<User>();
            }
            return null;
        }

        public async Task SaveUserAsync(User user)
        {
            if (string.IsNullOrEmpty(user.Id))
            {
                user.Id = Guid.NewGuid().ToString();
            }
            
            // Convert enum to string/int explicitly or let Firestore handle it if mapped properly.
            // Since we added [FirestoreData], FirestoreDb handles it natively.
            var docRef = _db.Collection("users").Document(user.Id);
            await docRef.SetAsync(user, SetOptions.MergeAll);
        }

        public async Task DeleteUserAsync(string id)
        {
            await _db.Collection("users").Document(id).DeleteAsync();
        }

        // ── Products ─────────────────────────────────────────────────────
        public async Task<List<Product>> GetProductsAsync()
        {
            var snapshot = await _db.Collection("products").GetSnapshotAsync();
            var products = new List<Product>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    products.Add(doc.ConvertTo<Product>());
                }
            }
            return products;
        }

        public async Task SaveProductAsync(Product product)
        {
            if (string.IsNullOrEmpty(product.Id))
            {
                product.Id = Guid.NewGuid().ToString();
            }
            product.UpdatedAt = DateTime.UtcNow;
            
            var docRef = _db.Collection("products").Document(product.Id);
            await docRef.SetAsync(product, SetOptions.MergeAll);
        }

        public async Task DeleteProductAsync(string id)
        {
            await _db.Collection("products").Document(id).DeleteAsync();
        }

        // ── Customers ────────────────────────────────────────────────────
        public async Task<List<Customer>> GetCustomersAsync()
        {
            var snapshot = await _db.Collection("customers").GetSnapshotAsync();
            var customers = new List<Customer>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    customers.Add(doc.ConvertTo<Customer>());
                }
            }
            return customers;
        }

        public async Task SaveCustomerAsync(Customer customer)
        {
            if (string.IsNullOrEmpty(customer.Id))
            {
                customer.Id = Guid.NewGuid().ToString();
            }
            customer.UpdatedAt = DateTime.UtcNow;
            
            var docRef = _db.Collection("customers").Document(customer.Id);
            await docRef.SetAsync(customer, SetOptions.MergeAll);
        }

        // ── Suppliers ────────────────────────────────────────────────────
        public async Task<List<Supplier>> GetSuppliersAsync()
        {
            var snapshot = await _db.Collection("suppliers").GetSnapshotAsync();
            var suppliers = new List<Supplier>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    suppliers.Add(doc.ConvertTo<Supplier>());
                }
            }
            return suppliers;
        }

        public async Task SaveSupplierAsync(Supplier supplier)
        {
            if (string.IsNullOrEmpty(supplier.Id))
            {
                supplier.Id = Guid.NewGuid().ToString();
            }
            supplier.UpdatedAt = DateTime.UtcNow;
            
            var docRef = _db.Collection("suppliers").Document(supplier.Id);
            await docRef.SetAsync(supplier, SetOptions.MergeAll);
        }

        public async Task DeleteSupplierAsync(string id)
        {
            await _db.Collection("suppliers").Document(id).DeleteAsync();
        }

        // ── Audit Logs ───────────────────────────────────────────────────
        public async Task<List<AuditLog>> GetAuditLogsAsync()
        {
            var query = _db.Collection("audit_logs").OrderByDescending("EventTime").Limit(100);
            var snapshot = await query.GetSnapshotAsync();
            var logs = new List<AuditLog>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    logs.Add(doc.ConvertTo<AuditLog>());
                }
            }
            return logs;
        }

        public async Task SaveAuditLogAsync(AuditLog log)
        {
            if (string.IsNullOrEmpty(log.Id))
            {
                log.Id = Guid.NewGuid().ToString();
            }
            
            var docRef = _db.Collection("audit_logs").Document(log.Id);
            await docRef.SetAsync(log, SetOptions.MergeAll);
        }
    }
}
