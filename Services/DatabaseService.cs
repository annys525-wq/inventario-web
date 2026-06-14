using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using InventarioApp.Models;

namespace InventarioApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public DatabaseService()
        {
            // Ubica la base de datos en la misma carpeta de ejecución del programa
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_database.db");
            _connectionString = $"Data Source={_dbPath}";
            InitializeDatabase();
        }

        public void InitializeDatabase()
        {
            bool isNew = !File.Exists(_dbPath);

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                // 1. Tabla de Usuarios
                string createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id TEXT PRIMARY KEY,
                        Username TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        FullName TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        Role INTEGER NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        CreatedAt TEXT NOT NULL
                    );";
                
                // 2. Tabla de Auditoría
                string createAuditTable = @"
                    CREATE TABLE IF NOT EXISTS AuditLogs (
                        Id TEXT PRIMARY KEY,
                        UserId TEXT NOT NULL,
                        Username TEXT NOT NULL,
                        EventTime TEXT NOT NULL,
                        EventType TEXT NOT NULL,
                        MachineName TEXT NOT NULL,
                        Description TEXT NOT NULL
                    );";

                // 3. Tabla de Cola de Sincronización (Outbox)
                string createSyncTable = @"
                    CREATE TABLE IF NOT EXISTS SyncQueue (
                        Id TEXT PRIMARY KEY,
                        TableName TEXT NOT NULL,
                        RecordId TEXT NOT NULL,
                        Operation TEXT NOT NULL,
                        PayloadJson TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL
                    );";

                // 4. Tabla de Productos
                string createProductsTable = @"
                    CREATE TABLE IF NOT EXISTS Products (
                        Id TEXT PRIMARY KEY,
                        SKU TEXT UNIQUE NOT NULL,
                        Name TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        Cost REAL NOT NULL,
                        Price REAL NOT NULL,
                        WarehouseMain INTEGER NOT NULL,
                        WarehouseSecondary INTEGER NOT NULL,
                        MinimumStock INTEGER NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NOT NULL
                    );";

                // 5. Tabla de Clientes (CRM)
                string createCustomersTable = @"
                    CREATE TABLE IF NOT EXISTS Customers (
                        Id TEXT PRIMARY KEY,
                        FullName TEXT NOT NULL,
                        TaxId TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        Phone TEXT NOT NULL,
                        PipelineStage TEXT NOT NULL,
                        CreditLimit REAL NOT NULL,
                        OutstandingBalance REAL NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NOT NULL
                    );";

                // 6. Tabla de Proveedores
                string createSuppliersTable = @"
                    CREATE TABLE IF NOT EXISTS Suppliers (
                        Id TEXT PRIMARY KEY,
                        FullName TEXT NOT NULL,
                        TaxId TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        Phone TEXT NOT NULL,
                        Address TEXT NOT NULL,
                        ContactPerson TEXT NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NOT NULL
                    );";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createUsersTable + createAuditTable + createSyncTable + createProductsTable + createCustomersTable + createSuppliersTable + createPurchasesTable + createPurchaseLinesTable;
                    command.ExecuteNonQuery();
                }

                if (isNew || GetUserCount() == 0)
                {
                    SeedData(connection);
                }
            }
        }

        private int GetUserCount()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM Users;";
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private void SeedData(SqliteConnection connection)
        {
            // Contraseña de prueba para todos los usuarios semilla (Hash SHA256 de 'admin123', 'vendedor123', 'bodega123')
            // admin123 hash: 24078914630b275b762b8004401565551db777271922c070494cf078dc115ff6
            // vendedor123 hash: cc8d6d678beffea126fb98f9a2631a0e10e6a147e448b309001b920bf803b984
            // bodega123 hash: fce7560b2eb3df163e7c8a410313f890251784ff9398863f6a2b8e3ad5d35a66

            string seedUsers = @"
                INSERT OR IGNORE INTO Users (Id, Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt) VALUES
                ('u1', 'admin', '24078914630b275b762b8004401565551db777271922c070494cf078dc115ff6', 'Administrador General', 'admin@empresa.com', 0, 1, '2026-05-26T00:00:00Z'),
                ('u2', 'vendedor', 'cc8d6d678beffea126fb98f9a2631a0e10e6a147e448b309001b920bf803b984', 'Juan Vendedor', 'juan.sales@empresa.com', 1, 1, '2026-05-26T00:00:00Z'),
                ('u3', 'bodega', 'fce7560b2eb3df163e7c8a410313f890251784ff9398863f6a2b8e3ad5d35a66', 'Marta Bodega', 'marta.inv@empresa.com', 2, 1, '2026-05-26T00:00:00Z');";

            string seedProducts = @"
                INSERT OR IGNORE INTO Products (Id, SKU, Name, Category, Cost, Price, WarehouseMain, WarehouseSecondary, MinimumStock, UpdatedAt, UpdatedBy) VALUES
                ('p1', 'PROD001', 'Computadora Portátil Core i7', 'Tecnología', 850.00, 1200.00, 15, 3, 5, '2026-05-26T00:00:00Z', 'Seed'),
                ('p2', 'PROD002', 'Monitor UltraWide 29""', 'Tecnología', 180.00, 270.00, 4, 1, 6, '2026-05-26T00:00:00Z', 'Seed'),
                ('p3', 'PROD003', 'Silla Ergonómica Pro', 'Mobiliario', 120.00, 195.00, 25, 10, 8, '2026-05-26T00:00:00Z', 'Seed'),
                ('p4', 'PROD004', 'Escritorio Elevable Eléctrico', 'Mobiliario', 310.00, 480.00, 2, 0, 3, '2026-05-26T00:00:00Z', 'Seed');";

            string seedCustomers = @"
                INSERT OR IGNORE INTO Customers (Id, FullName, TaxId, Email, Phone, PipelineStage, CreditLimit, OutstandingBalance, IsActive, UpdatedAt, UpdatedBy) VALUES
                ('c1', 'ACME Corp Colombia', '900.123.456-1', 'compras@acme.com.co', '+57 300 123 4567', 'Cerrado', 5000.00, 1250.00, 1, '2026-05-26T00:00:00Z', 'Seed'),
                ('c2', 'Distribuciones Globales S.A.S', '830.987.654-2', 'proveedores@global.com', '+57 315 987 6543', 'Propuesta', 12000.00, 0.00, 1, '2026-05-26T00:00:00Z', 'Seed'),
                ('c3', 'Industrias Metalmecánicas Luna', '901.444.888-0', 'contacto@metalluna.com', '+57 320 444 8888', 'Prospecto', 2000.00, 450.00, 1, '2026-05-26T00:00:00Z', 'Seed');";

            string seedSuppliers = @"
                INSERT OR IGNORE INTO Suppliers (Id, FullName, TaxId, Email, Phone, Address, ContactPerson, IsActive, UpdatedAt, UpdatedBy) VALUES
                ('prov1', 'Tecnología y Suministros S.A.', '890.222.111-4', 'contacto@tecno-suministros.com', '+57 311 222 3333', 'Av. 45 #88-12, Bogotá', 'Carlos Pérez', 1, '2026-05-26T00:00:00Z', 'Seed'),
                ('prov2', 'Distribuidora Mobiliaria Mayorista', '900.555.777-2', 'ventas@distrimobiliaria.co', '+57 314 999 8888', 'Calle 15 #30-45, Medellín', 'Ana María Gómez', 1, '2026-05-26T00:00:00Z', 'Seed');";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = seedUsers + seedProducts + seedCustomers + seedSuppliers;
                command.ExecuteNonQuery();
            }
        }

        #region CRUD - USUARIOS
        public List<User> GetUsers()
        {
            var list = new List<User>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt FROM Users;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new User
                            {
                                Id = reader.GetString(0),
                                Username = reader.GetString(1),
                                PasswordHash = reader.GetString(2),
                                FullName = reader.GetString(3),
                                Email = reader.GetString(4),
                                Role = (UserRole)reader.GetInt32(5),
                                IsActive = reader.GetInt32(6) == 1,
                                CreatedAt = DateTime.Parse(reader.GetString(7))
                            });
                        }
                    }
                }
            }
            return list;
        }

        public User? GetUserByUsername(string username)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt FROM Users WHERE Username = @username;";
                    command.Parameters.AddWithValue("@username", username.ToLower().Trim());
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new User
                            {
                                Id = reader.GetString(0),
                                Username = reader.GetString(1),
                                PasswordHash = reader.GetString(2),
                                FullName = reader.GetString(3),
                                Email = reader.GetString(4),
                                Role = (UserRole)reader.GetInt32(5),
                                IsActive = reader.GetInt32(6) == 1,
                                CreatedAt = DateTime.Parse(reader.GetString(7))
                            };
                        }
                    }
                }
            }
            return null;
        }

        public void SaveUser(User user)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT OR REPLACE INTO Users (Id, Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                        VALUES (@id, @username, @passwordHash, @fullName, @email, @role, @isActive, @createdAt);";
                    command.Parameters.AddWithValue("@id", user.Id);
                    command.Parameters.AddWithValue("@username", user.Username.ToLower().Trim());
                    command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
                    command.Parameters.AddWithValue("@fullName", user.FullName);
                    command.Parameters.AddWithValue("@email", user.Email);
                    command.Parameters.AddWithValue("@role", (int)user.Role);
                    command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
                    command.Parameters.AddWithValue("@createdAt", user.CreatedAt.ToString("o"));
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteUser(string id)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM Users WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region CRUD - PRODUCTOS
        public List<Product> GetProducts()
        {
            var list = new List<Product>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, SKU, Name, Category, Cost, Price, WarehouseMain, WarehouseSecondary, MinimumStock, UpdatedAt, UpdatedBy FROM Products;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Product
                            {
                                Id = reader.GetString(0),
                                SKU = reader.GetString(1),
                                Name = reader.GetString(2),
                                Category = reader.GetString(3),
                                Cost = Convert.ToDecimal(reader.GetDouble(4)),
                                Price = Convert.ToDecimal(reader.GetDouble(5)),
                                WarehouseMain = reader.GetInt32(6),
                                WarehouseSecondary = reader.GetInt32(7),
                                MinimumStock = reader.GetInt32(8),
                                UpdatedAt = DateTime.Parse(reader.GetString(9)),
                                UpdatedBy = reader.GetString(10)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public void SaveProduct(Product product)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT OR REPLACE INTO Products (Id, SKU, Name, Category, Cost, Price, WarehouseMain, WarehouseSecondary, MinimumStock, UpdatedAt, UpdatedBy)
                        VALUES (@id, @sku, @name, @category, @cost, @price, @wMain, @wSec, @minStock, @updatedAt, @updatedBy);";
                    command.Parameters.AddWithValue("@id", product.Id);
                    command.Parameters.AddWithValue("@sku", product.SKU);
                    command.Parameters.AddWithValue("@name", product.Name);
                    command.Parameters.AddWithValue("@category", product.Category);
                    command.Parameters.AddWithValue("@cost", (double)product.Cost);
                    command.Parameters.AddWithValue("@price", (double)product.Price);
                    command.Parameters.AddWithValue("@wMain", product.WarehouseMain);
                    command.Parameters.AddWithValue("@wSec", product.WarehouseSecondary);
                    command.Parameters.AddWithValue("@minStock", product.MinimumStock);
                    command.Parameters.AddWithValue("@updatedAt", product.UpdatedAt.ToString("o"));
                    command.Parameters.AddWithValue("@updatedBy", product.UpdatedBy);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteProduct(string id)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM Products WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region CRUD - PROVEEDORES
        public List<Supplier> GetSuppliers()
        {
            var list = new List<Supplier>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, FullName, TaxId, Email, Phone, Address, ContactPerson, IsActive, UpdatedAt, UpdatedBy FROM Suppliers;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Supplier
                            {
                                Id = reader.GetString(0),
                                FullName = reader.GetString(1),
                                TaxId = reader.GetString(2),
                                Email = reader.GetString(3),
                                Phone = reader.GetString(4),
                                Address = reader.GetString(5),
                                ContactPerson = reader.GetString(6),
                                IsActive = reader.GetInt32(7) == 1,
                                UpdatedAt = DateTime.Parse(reader.GetString(8)),
                                UpdatedBy = reader.GetString(9)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public void SaveSupplier(Supplier supplier)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT OR REPLACE INTO Suppliers (Id, FullName, TaxId, Email, Phone, Address, ContactPerson, IsActive, UpdatedAt, UpdatedBy)
                        VALUES (@id, @fullName, @taxId, @email, @phone, @address, @contactPerson, @isActive, @updatedAt, @updatedBy);";
                    command.Parameters.AddWithValue("@id", supplier.Id);
                    command.Parameters.AddWithValue("@fullName", supplier.FullName);
                    command.Parameters.AddWithValue("@taxId", supplier.TaxId);
                    command.Parameters.AddWithValue("@email", supplier.Email);
                    command.Parameters.AddWithValue("@phone", supplier.Phone);
                    command.Parameters.AddWithValue("@address", supplier.Address);
                    command.Parameters.AddWithValue("@contactPerson", supplier.ContactPerson);
                    command.Parameters.AddWithValue("@isActive", supplier.IsActive ? 1 : 0);
                    command.Parameters.AddWithValue("@updatedAt", supplier.UpdatedAt.ToString("o"));
                    command.Parameters.AddWithValue("@updatedBy", supplier.UpdatedBy);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteSupplier(string id)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM Suppliers WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region CRUD - CLIENTES
        public List<Customer> GetCustomers()
        {
            var list = new List<Customer>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, FullName, TaxId, Email, Phone, PipelineStage, CreditLimit, OutstandingBalance, IsActive, UpdatedAt, UpdatedBy FROM Customers;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Customer
                            {
                                Id = reader.GetString(0),
                                FullName = reader.GetString(1),
                                TaxId = reader.GetString(2),
                                Email = reader.GetString(3),
                                Phone = reader.GetString(4),
                                PipelineStage = reader.GetString(5),
                                CreditLimit = Convert.ToDecimal(reader.GetDouble(6)),
                                OutstandingBalance = Convert.ToDecimal(reader.GetDouble(7)),
                                IsActive = reader.GetInt32(8) == 1,
                                UpdatedAt = DateTime.Parse(reader.GetString(9)),
                                UpdatedBy = reader.GetString(10)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public void SaveCustomer(Customer customer)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT OR REPLACE INTO Customers (Id, FullName, TaxId, Email, Phone, PipelineStage, CreditLimit, OutstandingBalance, IsActive, UpdatedAt, UpdatedBy)
                        VALUES (@id, @fullName, @taxId, @email, @phone, @pipeline, @credit, @balance, @isActive, @updatedAt, @updatedBy);";
                    command.Parameters.AddWithValue("@id", customer.Id);
                    command.Parameters.AddWithValue("@fullName", customer.FullName);
                    command.Parameters.AddWithValue("@taxId", customer.TaxId);
                    command.Parameters.AddWithValue("@email", customer.Email);
                    command.Parameters.AddWithValue("@phone", customer.Phone);
                    command.Parameters.AddWithValue("@pipeline", customer.PipelineStage);
                    command.Parameters.AddWithValue("@credit", (double)customer.CreditLimit);
                    command.Parameters.AddWithValue("@balance", (double)customer.OutstandingBalance);
                    command.Parameters.AddWithValue("@isActive", customer.IsActive ? 1 : 0);
                    command.Parameters.AddWithValue("@updatedAt", customer.UpdatedAt.ToString("o"));
                    command.Parameters.AddWithValue("@updatedBy", customer.UpdatedBy);
                    command.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region AUDITORÍA Y LOGS
        public List<AuditLog> GetAuditLogs()
        {
            var list = new List<AuditLog>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, UserId, Username, EventTime, EventType, MachineName, Description FROM AuditLogs ORDER BY EventTime DESC;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new AuditLog
                            {
                                Id = reader.GetString(0),
                                UserId = reader.GetString(1),
                                Username = reader.GetString(2),
                                EventTime = DateTime.Parse(reader.GetString(3)),
                                EventType = reader.GetString(4),
                                MachineName = reader.GetString(5),
                                Description = reader.GetString(6)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public void SaveAuditLog(AuditLog log)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO AuditLogs (Id, UserId, Username, EventTime, EventType, MachineName, Description)
                        VALUES (@id, @userId, @username, @eventTime, @eventType, @machine, @desc);";
                    command.Parameters.AddWithValue("@id", log.Id);
                    command.Parameters.AddWithValue("@userId", log.UserId);
                    command.Parameters.AddWithValue("@username", log.Username);
                    command.Parameters.AddWithValue("@eventTime", log.EventTime.ToString("o"));
                    command.Parameters.AddWithValue("@eventType", log.EventType);
                    command.Parameters.AddWithValue("@machine", log.MachineName);
                    command.Parameters.AddWithValue("@desc", log.Description);
                    command.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region COLA DE SINCRONIZACIÓN (OUTBOX)
        public List<SyncItem> GetSyncQueue()
        {
            var list = new List<SyncItem>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, TableName, RecordId, Operation, PayloadJson, CreatedAt FROM SyncQueue ORDER BY CreatedAt ASC;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new SyncItem
                            {
                                Id = reader.GetString(0),
                                TableName = reader.GetString(1),
                                RecordId = reader.GetString(2),
                                Operation = reader.GetString(3),
                                PayloadJson = reader.GetString(4),
                                CreatedAt = DateTime.Parse(reader.GetString(5))
                            });
                        }
                    }
                }
            }
            return list;
        }

        public void EnqueueSyncItem(SyncItem item)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO SyncQueue (Id, TableName, RecordId, Operation, PayloadJson, CreatedAt)
                        VALUES (@id, @table, @recordId, @op, @payload, @createdAt);";
                    command.Parameters.AddWithValue("@id", item.Id);
                    command.Parameters.AddWithValue("@table", item.TableName);
                    command.Parameters.AddWithValue("@recordId", item.RecordId);
                    command.Parameters.AddWithValue("@op", item.Operation);
                    command.Parameters.AddWithValue("@payload", item.PayloadJson);
                    command.Parameters.AddWithValue("@createdAt", item.CreatedAt.ToString("o"));
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteSyncItem(string id)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM SyncQueue WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }
        //anamaria
        public void SeedAdminUser()
        {
            using var con = new SQLiteConnection(_connectionString);
            con.Open();

            // Crea la tabla si no existe
            con.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL
                );");

            // Si no hay admin, lo inserta
            var exists = con.QuerySingleOrDefault<int>(
                "SELECT COUNT(1) FROM Users WHERE Username = @u",
                new { u = "admin" });

            if (exists == 0)
            {
                var hash = BCrypt.Net.BCrypt.HashPassword("admin123");
                con.Execute(
                    "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)",
                    new { u = "admin", p = hash });
            }
            var db = builder.Services.BuildServiceProvider()
             .GetRequiredService<DatabaseService>();
db.SeedAdminUser();

        }

        #endregion
        


    }
}
