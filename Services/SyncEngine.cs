using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using InventarioApp.Models;

namespace InventarioApp.Services
{
    public enum ConflictResolution
    {
        UseLocal,
        UseRemote
    }

    public class ConflictEventArgs : EventArgs
    {
        public string SyncItemId { get; }
        public string TableName { get; }
        public string RecordId { get; }
        public string LocalDataJson { get; }
        public string RemoteDataJson { get; }
        public TaskCompletionSource<ConflictResolution> ResolutionSource { get; }

        public ConflictEventArgs(string syncItemId, string tableName, string recordId, string localDataJson, string remoteDataJson, TaskCompletionSource<ConflictResolution> resolutionSource)
        {
            SyncItemId = syncItemId;
            TableName = tableName;
            RecordId = recordId;
            LocalDataJson = localDataJson;
            RemoteDataJson = remoteDataJson;
            ResolutionSource = resolutionSource;
        }
    }

    public class SyncEngine
    {
        private readonly DatabaseService _db;
        private readonly FirestoreService _firestore;
        private readonly AuditService _audit;

        public event EventHandler<ConflictEventArgs>? ConflictDetected;
        public event EventHandler<string>? SyncStatusChanged;

        public SyncEngine(DatabaseService db, FirestoreService firestore, AuditService audit)
        {
            _db = db;
            _firestore = firestore;
            _audit = audit;
        }

        // Ejecuta la sincronización de la cola local (Outbox) con la nube (Firestore)
        public async Task<int> SyncOutboxAsync()
        {
            if (!_firestore.IsSimulatedOnline)
            {
                SyncStatusChanged?.Invoke(this, "Offline - Sincronización omitida");
                return 0;
            }

            SyncStatusChanged?.Invoke(this, "Conectando y analizando cola...");
            var queue = _db.GetSyncQueue();
            if (queue.Count == 0)
            {
                SyncStatusChanged?.Invoke(this, "Sistema al día (0 pendientes)");
                return 0;
            }

            int syncedCount = 0;
            SyncStatusChanged?.Invoke(this, $"Sincronizando {queue.Count} transacciones...");

            foreach (var item in queue)
            {
                try
                {
                    bool success = await ProcessQueueItemAsync(item);
                    if (success)
                    {
                        _db.DeleteSyncItem(item.Id);
                        syncedCount++;
                        SyncStatusChanged?.Invoke(this, $"Sincronizados: {syncedCount}/{queue.Count}");
                    }
                    else
                    {
                        // Sincronización detenida o diferida por conflicto pendiente de resolución
                        break;
                    }
                }
                catch (Exception ex)
                {
                    SyncStatusChanged?.Invoke(this, $"Error en transacción: {ex.Message}");
                    _audit.LogSecurityEvent("System", "SyncEngine", "Sync_Error", $"Error sincronizando {item.TableName} (ID: {item.RecordId}): {ex.Message}");
                    break; // Detener flujo ante error crítico de red
                }
            }

            SyncStatusChanged?.Invoke(this, $"Sincronización finalizada. {syncedCount} items procesados.");
            return syncedCount;
        }

        private async Task<bool> ProcessQueueItemAsync(SyncItem item)
        {
            string collectionName = item.TableName; // Colección de Firestore
            
            // Si la operación es DELETE, se replica directamente
            if (item.Operation == "DELETE")
            {
                await _firestore.DeleteCloudDocumentAsync(collectionName, item.RecordId);
                return true;
            }

            // Consultar si el registro existe en la nube para comprobar conflictos de concurrencia
            var remoteDoc = _firestore.GetCloudDocument(collectionName, item.RecordId);
            
            if (remoteDoc != null)
            {
                // El registro existe en la nube. Comprobamos si tiene una fecha de modificación más reciente
                // Para simplificar, leemos el campo 'UpdatedAt' de ambos lados
                DateTime remoteUpdatedAt = DateTime.MinValue;
                if (remoteDoc.TryGetValue("UpdatedAt", out object? remoteDateObj) && remoteDateObj != null)
                {
                    DateTime.TryParse(remoteDateObj.ToString(), out remoteUpdatedAt);
                }

                // Deserializar el payload local
                var localFields = JsonSerializer.Deserialize<Dictionary<string, object>>(item.PayloadJson);
                DateTime localUpdatedAt = DateTime.MinValue;
                if (localFields != null && localFields.TryGetValue("UpdatedAt", out object? localDateObj) && localDateObj != null)
                {
                    DateTime.TryParse(localDateObj.ToString(), out localUpdatedAt);
                }

                // Hay conflicto de concurrencia si el servidor fue modificado después que la base local
                // y los datos difieren
                if (remoteUpdatedAt > localUpdatedAt && !ArePayloadsEqual(localFields, remoteDoc))
                {
                    _audit.LogSecurityEvent("System", "SyncEngine", "Conflict_Detected", $"Conflicto detectado en la tabla {item.TableName} para el registro ID: {item.RecordId}");

                    // Lanzar el evento a la interfaz para resolución interactiva
                    var tcs = new TaskCompletionSource<ConflictResolution>();
                    
                    ConflictDetected?.Invoke(this, new ConflictEventArgs(
                        item.Id,
                        item.TableName,
                        item.RecordId,
                        item.PayloadJson,
                        JsonSerializer.Serialize(remoteDoc),
                        tcs
                    ));

                    // Esperar a que el usuario decida en la UI
                    ConflictResolution resolution = await tcs.Task;

                    if (resolution == ConflictResolution.UseRemote)
                    {
                        // Opción "Usar nube": Aplicamos los datos remotos en la base SQLite local
                        ApplyCloudDataToLocal(item.TableName, item.RecordId, remoteDoc);
                        _audit.LogSecurityEvent("System", "SyncEngine", "Conflict_Resolved", $"Conflicto resuelto: Se conservaron datos remotos (Server Wins) en {item.TableName} ID: {item.RecordId}");
                        return true; // El conflicto se resolvió aplicando la nube localmente, podemos saltar esta subida
                    }
                    else
                    {
                        // Opción "Usar local": Forzamos la versión local a la nube
                        _audit.LogSecurityEvent("System", "SyncEngine", "Conflict_Resolved", $"Conflicto resuelto: Se forzó la versión local (Client Wins) en {item.TableName} ID: {item.RecordId}");
                        if (localFields != null)
                        {
                            await _firestore.SaveCloudDocumentAsync(collectionName, item.RecordId, localFields);
                        }
                        return true;
                    }
                }
            }

            // Si no hay conflicto, subir directamente el payload local a la nube
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(item.PayloadJson);
            if (payload != null)
            {
                await _firestore.SaveCloudDocumentAsync(collectionName, item.RecordId, payload);
            }
            return true;
        }

        private bool ArePayloadsEqual(Dictionary<string, object>? local, Dictionary<string, object> remote)
        {
            if (local == null) return false;
            // Verificamos los campos comunes (ignorando campos de control de sincronización si se desea)
            foreach (var key in local.Keys)
            {
                if (key == "UpdatedAt" || key == "UpdatedBy") continue;
                
                if (remote.TryGetValue(key, out object? remoteVal))
                {
                    string localStr = local[key]?.ToString() ?? "";
                    string remoteStr = remoteVal?.ToString() ?? "";
                    if (localStr != remoteStr) return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void ApplyCloudDataToLocal(string tableName, string recordId, Dictionary<string, object> remoteFields)
        {
            string json = JsonSerializer.Serialize(remoteFields);
            
            if (tableName == "Users")
            {
                var user = JsonSerializer.Deserialize<User>(json);
                if (user != null) _db.SaveUser(user);
            }
            else if (tableName == "Products")
            {
                var prod = JsonSerializer.Deserialize<Product>(json);
                if (prod != null) _db.SaveProduct(prod);
            }
            else if (tableName == "Customers")
            {
                var cust = JsonSerializer.Deserialize<Customer>(json);
                if (cust != null) _db.SaveCustomer(cust);
            }
        }
    }
}
