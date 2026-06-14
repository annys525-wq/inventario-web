using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace InventarioApp.Services
{
    public class FirestoreService
    {
        private readonly string _cloudDbPath;
        private readonly string _connectionString;
        
        public bool IsSimulatedOnline { get; set; } = true;
        public int SimulatedLatencyMs { get; set; } = 800; // Latencia típica de red en milisegundos

        public FirestoreService()
        {
            // La base de datos en la nube simulada se almacena en un archivo separado para simular un servidor físico independiente
            _cloudDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloud_simulator.db");
            _connectionString = $"Data Source={_cloudDbPath}";
            InitializeCloudSchema();
        }

        private void InitializeCloudSchema()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                string createCloudTables = @"
                    CREATE TABLE IF NOT EXISTS CloudUsers (
                        Id TEXT PRIMARY KEY,
                        Username TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        FullName TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        Role INTEGER NOT NULL,
                        IsActive INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS CloudProducts (
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
                    );
                    CREATE TABLE IF NOT EXISTS CloudCustomers (
                        Id TEXT PRIMARY KEY,
                        FullName TEXT NOT NULL,
                        TaxId TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        Phone TEXT NOT NULL,
                        PipelineStage TEXT NOT NULL,
                        CreditLimit REAL NOT NULL,
                        OutstandingBalance REAL NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS CloudSuppliers (
                        Id TEXT PRIMARY KEY,
                        FullName TEXT NOT NULL,
                        TaxId TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        Phone TEXT NOT NULL,
                        Address TEXT NOT NULL,
                        ContactPerson TEXT NOT NULL,
                        IsActive INTEGER NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS CloudAuditLogs (
                        Id TEXT PRIMARY KEY,
                        UserId TEXT NOT NULL,
                        Username TEXT NOT NULL,
                        EventTime TEXT NOT NULL,
                        EventType TEXT NOT NULL,
                        MachineName TEXT NOT NULL,
                        Description TEXT NOT NULL
                    );";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = createCloudTables;
                    command.ExecuteNonQuery();
                }

                // Si la nube simulada está vacía, sembramos los mismos usuarios iniciales
                if (GetCloudUserCount() == 0)
                {
                    SeedCloudData(connection);
                }
            }
        }

        private int GetCloudUserCount()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM CloudUsers;";
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private void SeedCloudData(SqliteConnection connection)
        {
            string seedUsers = @"
                INSERT OR IGNORE INTO CloudUsers (Id, Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt, UpdatedAt) VALUES
                ('u1', 'admin', '24078914630b275b762b8004401565551db777271922c070494cf078dc115ff6', 'Administrador General', 'admin@empresa.com', 0, 1, '2026-05-26T00:00:00Z', '2026-05-26T00:00:00Z'),
                ('u2', 'vendedor', 'cc8d6d678beffea126fb98f9a2631a0e10e6a147e448b309001b920bf803b984', 'Juan Vendedor', 'juan.sales@empresa.com', 1, 1, '2026-05-26T00:00:00Z', '2026-05-26T00:00:00Z'),
                ('u3', 'bodega', 'fce7560b2eb3df163e7c8a410313f890251784ff9398863f6a2b8e3ad5d35a66', 'Marta Bodega', 'marta.inv@empresa.com', 2, 1, '2026-05-26T00:00:00Z', '2026-05-26T00:00:00Z');";

            string seedProducts = @"
                INSERT OR IGNORE INTO CloudProducts (Id, SKU, Name, Category, Cost, Price, WarehouseMain, WarehouseSecondary, MinimumStock, UpdatedAt, UpdatedBy) VALUES
                ('p1', 'PROD001', 'Computadora Portátil Core i7', 'Tecnología', 850.00, 2200000.00, 15, 3, 5, '2026-05-26T00:00:00Z', 'Seed'),
                ('p2', 'PROD002', 'Monitor UltraWide 29""', 'Tecnología', 180.00, 8700000.00, 4, 1, 6, '2026-05-26T00:00:00Z', 'Seed'),
                ('p3', 'PROD003', 'Silla Ergonómica Pro', 'Mobiliario', 120.00, 195.00, 25, 10, 8, '2026-05-26T00:00:00Z', 'Seed'),
                ('p4', 'PROD004', 'Escritorio Elevable Eléctrico', 'Mobiliario', 310000.00, 480.00, 2, 0, 3, '2026-05-26T00:00:00Z', 'Seed');";

            string seedCustomers = @"
                INSERT OR IGNORE INTO CloudCustomers (Id, FullName, TaxId, Email, Phone, PipelineStage, CreditLimit, OutstandingBalance, UpdatedAt, UpdatedBy) VALUES
                ('c1', 'ACME Corp Colombia', '900.123.456-1', 'compras@acme.com.co', '+57 300 123 4567', 'Cerrado', 5000.00, 1250.00, '2026-05-26T00:00:00Z', 'Seed'),
                ('c2', 'Distribuciones Globales S.A.S', '830.987.654-2', 'proveedores@global.com', '+57 315 987 6543', 'Propuesta', 12000.00, 0.00, '2026-05-26T00:00:00Z', 'Seed'),
                ('c3', 'Industrias Metalmecánicas Luna', '901.444.888-0', 'contacto@metalluna.com', '+57 320 444 8888', 'Prospecto', 2000.00, 450.00, '2026-05-26T00:00:00Z', 'Seed');";

            string seedSuppliers = @"
                INSERT OR IGNORE INTO CloudSuppliers (Id, FullName, TaxId, Email, Phone, Address, ContactPerson, IsActive, UpdatedAt, UpdatedBy) VALUES
                ('prov1', 'Tecnología y Suministros S.A.', '890.222.111-4', 'contacto@tecno-suministros.com', '+57 311 222 3333', 'Av. 45 #88-12, Bogotá', 'Carlos Pérez', 1, '2026-05-26T00:00:00Z', 'Seed'),
                ('prov2', 'Distribuidora Mobiliaria Mayorista', '900.555.777-2', 'ventas@distrimobiliaria.co', '+57 314 999 8888', 'Calle 15 #30-45, Medellín', 'Ana María Gómez', 1, '2026-05-26T00:00:00Z', 'Seed');";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = seedUsers + seedProducts + seedCustomers + seedSuppliers;
                command.ExecuteNonQuery();
            }
        }

        #region MÉTODOS DE SIMULACIÓN DE NUBE (CON LATENCIA)
        private async Task SimulateNetwork()
        {
            if (!IsSimulatedOnline)
            {
                throw new InvalidOperationException("No se pudo establecer conexión con Firebase Firestore. Error de red.");
            }
            if (SimulatedLatencyMs > 0)
            {
                await Task.Delay(SimulatedLatencyMs);
            }
        }

        public async Task<List<Dictionary<string, object>>> GetCloudCollectionAsync(string collectionName)
        {
            await SimulateNetwork();
            var list = new List<Dictionary<string, object>>();
            string tableName = "Cloud" + collectionName;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM {tableName};";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var doc = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                doc[reader.GetName(i)] = reader.GetValue(i);
                            }
                            list.Add(doc);
                        }
                    }
                }
            }
            return list;
        }

        public async Task SaveCloudDocumentAsync(string collectionName, string id, Dictionary<string, object> fields)
        {
            await SimulateNetwork();
            string tableName = "Cloud" + collectionName;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                
                // Primero construimos la consulta INSERT OR REPLACE dinámicamente
                var columns = new List<string>();
                var parameters = new List<string>();
                
                foreach (var kvp in fields)
                {
                    columns.Add(kvp.Key);
                    parameters.Add("@" + kvp.Key);
                }

                string query = $"INSERT OR REPLACE INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)});";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    foreach (var kvp in fields)
                    {
                        command.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
                    }
                    command.ExecuteNonQuery();
                }
            }
        }

        public async Task DeleteCloudDocumentAsync(string collectionName, string id)
        {
            await SimulateNetwork();
            string tableName = "Cloud" + collectionName;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"DELETE FROM {tableName} WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }

        // Permite forzar un cambio directamente en la base simulada "en la nube" para provocar conflictos intencionados
        public void ForceRemoteConflict(string collectionName, string recordId, string fieldName, object newValue)
        {
            string tableName = "Cloud" + collectionName;
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"UPDATE {tableName} SET {fieldName} = @val, UpdatedAt = @now WHERE Id = @id;";
                    command.Parameters.AddWithValue("@val", newValue);
                    command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@id", recordId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public Dictionary<string, object>? GetCloudDocument(string collectionName, string id)
        {
            string tableName = "Cloud" + collectionName;
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM {tableName} WHERE Id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var doc = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                doc[reader.GetName(i)] = reader.GetValue(i);
                            }
                            return doc;
                        }
                    }
                }
            }
            return null;
        }
        #endregion
    }
}
