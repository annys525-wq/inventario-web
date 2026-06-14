using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using InventarioApp.Models;
using InventarioApp.Services;

namespace InventarioApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private readonly AuthService _auth;
        private readonly FirestoreService _firestore;
        private readonly SyncEngine _sync;
        private readonly AuditService _audit;

        private object _currentView;
        private string _activeViewName = "Dashboard";
        private string _networkState = "Online";
        private int _outboxCount;
        private string _syncStatusMessage = "Sistema listo.";
        
        // Propiedades para la resolución de conflictos interactiva
        private bool _isConflictOverlayVisible;
        private string _conflictTableName = string.Empty;
        private string _conflictRecordId = string.Empty;
        private string _conflictLocalData = string.Empty;
        private string _conflictRemoteData = string.Empty;
        private TaskCompletionSource<ConflictResolution>? _activeConflictTcs;

        // ViewModels secundarios lazily initialized
        private UserManagementViewModel? _userManagementVm;

        private System.Collections.ObjectModel.ObservableCollection<Product> _productsList = new();
        private System.Collections.ObjectModel.ObservableCollection<Customer> _customersList = new();
        private System.Collections.ObjectModel.ObservableCollection<AuditLog> _auditLogsList = new();

        public User? CurrentUser => _auth.CurrentUser;
        
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string ActiveViewName
        {
            get => _activeViewName;
            set => SetProperty(ref _activeViewName, value);
        }

        public string NetworkState
        {
            get => _networkState;
            set => SetProperty(ref _networkState, value);
        }

        public int OutboxCount
        {
            get => _outboxCount;
            set => SetProperty(ref _outboxCount, value);
        }

        public string SyncStatusMessage
        {
            get => _syncStatusMessage;
            set => SetProperty(ref _syncStatusMessage, value);
        }

        // Resolución de conflictos
        public bool IsConflictOverlayVisible
        {
            get => _isConflictOverlayVisible;
            set => SetProperty(ref _isConflictOverlayVisible, value);
        }

        public string ConflictTableName
        {
            get => _conflictTableName;
            set => SetProperty(ref _conflictTableName, value);
        }

        public string ConflictRecordId
        {
            get => _conflictRecordId;
            set => SetProperty(ref _conflictRecordId, value);
        }

        public string ConflictLocalData
        {
            get => _conflictLocalData;
            set => SetProperty(ref _conflictLocalData, value);
        }

        public string ConflictRemoteData
        {
            get => _conflictRemoteData;
            set => SetProperty(ref _conflictRemoteData, value);
        }

        public System.Collections.ObjectModel.ObservableCollection<Product> ProductsList
        {
            get => _productsList;
            set => SetProperty(ref _productsList, value);
        }

        public System.Collections.ObjectModel.ObservableCollection<Customer> CustomersList
        {
            get => _customersList;
            set => SetProperty(ref _customersList, value);
        }

        public System.Collections.ObjectModel.ObservableCollection<AuditLog> AuditLogsList
        {
            get => _auditLogsList;
            set => SetProperty(ref _auditLogsList, value);
        }

        // Comandos de navegación y acciones
        public ICommand NavigateCommand { get; }
        public ICommand ToggleNetworkCommand { get; }
        public ICommand SyncNowCommand { get; }
        public ICommand LogoutCommand { get; }
        
        // Comandos de resolución de conflicto
        public ICommand ResolveUseLocalCommand { get; }
        public ICommand ResolveUseRemoteCommand { get; }

        // Comando para forzar un conflicto simulado
        public ICommand ForceCloudConflictCommand { get; }

        public MainViewModel(DatabaseService db, AuthService auth, FirestoreService firestore, SyncEngine sync, AuditService audit)
        {
            _db = db;
            _auth = auth;
            _firestore = firestore;
            _sync = sync;
            _audit = audit;

            // Iniciar vista en Dashboard (representado como un string o view model básico)
            _currentView = "Dashboard"; 

            NavigateCommand = new RelayCommand(ExecuteNavigate);
            ToggleNetworkCommand = new RelayCommand(_ => ExecuteToggleNetwork());
            SyncNowCommand = new RelayCommand(async _ => await ExecuteSyncNowAsync());
            LogoutCommand = new RelayCommand(_ => ExecuteLogout());

            ResolveUseLocalCommand = new RelayCommand(_ => ResolveConflict(ConflictResolution.UseLocal));
            ResolveUseRemoteCommand = new RelayCommand(_ => ResolveConflict(ConflictResolution.UseRemote));
            ForceCloudConflictCommand = new RelayCommand(_ => ExecuteForceCloudConflict());

            // Suscribirse a eventos del SyncEngine
            _sync.SyncStatusChanged += (s, msg) => SyncStatusMessage = msg;
            _sync.ConflictDetected += HandleSyncConflict;

            RefreshOutboxCount();
        }

        public void RefreshOutboxCount()
        {
            try
            {
                OutboxCount = _db.GetSyncQueue().Count;
            }
            catch
            {
                OutboxCount = 0;
            }
        }

        private void ExecuteNavigate(object? parameter)
        {
            string viewName = parameter?.ToString() ?? "Dashboard";

            if (viewName == "Usuarios")
            {
                if (!_auth.CheckAuthorize("USER_CRUD"))
                {
                    MessageBox.Show("Acceso denegado. Este módulo requiere privilegios de Administrador.", "Permisos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    
                    // Registrar el intento fallido
                    _audit.LogSecurityEvent(_auth.CurrentUser?.Id ?? "UNKNOWN", _auth.CurrentUser?.Username ?? "Desconocido", "Access_Denied", "Intento de navegación no autorizado al panel de usuarios.");
                    return;
                }

                if (_userManagementVm == null)
                {
                    _userManagementVm = new UserManagementViewModel(_db, _auth, _audit);
                }
                else
                {
                    _userManagementVm.LoadData(); // Recargar datos frescos
                }
                CurrentView = _userManagementVm;
                ActiveViewName = "Usuarios";
            }
            else if (viewName == "Dashboard")
            {
                CurrentView = "Dashboard";
                ActiveViewName = "Dashboard";
                RefreshOutboxCount();
            }
            else if (viewName == "Inventario")
            {
                CurrentView = "Inventario";
                ActiveViewName = "Inventario";
                ProductsList = new System.Collections.ObjectModel.ObservableCollection<Product>(_db.GetProducts());
            }
            else if (viewName == "CRM")
            {
                CurrentView = "CRM";
                ActiveViewName = "CRM";
                CustomersList = new System.Collections.ObjectModel.ObservableCollection<Customer>(_db.GetCustomers());
            }
            else if (viewName == "Auditoria")
            {
                if (!_auth.CheckAuthorize("VIEW_AUDIT_LOGS"))
                {
                    MessageBox.Show("Acceso denegado. Este módulo requiere privilegios de Administrador.", "Permisos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                CurrentView = "Auditoria";
                ActiveViewName = "Auditoria";
                AuditLogsList = new System.Collections.ObjectModel.ObservableCollection<AuditLog>(_db.GetAuditLogs());
            }
        }

        private void ExecuteToggleNetwork()
        {
            _firestore.IsSimulatedOnline = !_firestore.IsSimulatedOnline;
            NetworkState = _firestore.IsSimulatedOnline ? "Online" : "Offline";
            SyncStatusMessage = NetworkState == "Online" ? "Conectado. Listo para sincronizar." : "Modo fuera de línea activado.";

            // Si pasa a Online, intentar sincronizar automáticamente (Offline-first behavior)
            if (_firestore.IsSimulatedOnline)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000); // Pequeña pausa
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        await ExecuteSyncNowAsync();
                    });
                });
            }
        }

        public async Task ExecuteSyncNowAsync()
        {
            RefreshOutboxCount();
            await _sync.SyncOutboxAsync();
            RefreshOutboxCount();
        }

        private void ExecuteLogout()
        {
            _auth.Logout();
            
            // Cerrar MainWindow y abrir LoginWindow
            var currentWindow = Application.Current.MainWindow;
            
            // Inicializar nueva login window
            var loginWin = new Views.LoginWindow();
            var loginVm = new LoginViewModel(_auth);
            loginVm.LoginSuccess += () =>
            {
                var mainWin = new MainWindow();
                mainWin.DataContext = this;
                Application.Current.MainWindow = mainWin;
                mainWin.Show();
                loginWin.Close();
            };
            loginWin.DataContext = loginVm;
            
            loginWin.Show();
            currentWindow?.Close();
        }

        private void HandleSyncConflict(object? sender, ConflictEventArgs e)
        {
            // Detener el hilo y pasar la info al overlay visual
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConflictTableName = e.TableName;
                ConflictRecordId = e.RecordId;
                ConflictLocalData = FormatJsonString(e.LocalDataJson);
                ConflictRemoteData = FormatJsonString(e.RemoteDataJson);
                _activeConflictTcs = e.ResolutionSource;
                IsConflictOverlayVisible = true;
            });
        }

        private void ResolveConflict(ConflictResolution resolution)
        {
            if (_activeConflictTcs != null)
            {
                _activeConflictTcs.SetResult(resolution);
                _activeConflictTcs = null;
                IsConflictOverlayVisible = false;
                
                // Refrescar recuento tras aplicar cambios
                Task.Run(async () =>
                {
                    await Task.Delay(500);
                    Application.Current.Dispatcher.Invoke(() => {
                        RefreshOutboxCount();
                        if (CurrentView is UserManagementViewModel uvm) uvm.LoadData();
                    });
                });
            }
        }

        private void ExecuteForceCloudConflict()
        {
            if (!_firestore.IsSimulatedOnline)
            {
                MessageBox.Show("Activa el simulador de red (Online) para forzar un conflicto en la nube.", "Simulación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Vamos a forzar una modificación directa del usuario semilla 'u2' (juan vendedor) en la NUBE simulada
                // Esto simulará que otro dispositivo modificó a 'u2' mientras este cliente estaba offline o trabajando.
                _firestore.ForceRemoteConflict("Users", "u2", "FullName", "Juan Vendedor Modificado en Nube");
                
                // Y ahora agregamos un cambio local en el outbox para el mismo usuario 'u2'
                var u2Local = _db.GetUserByUsername("vendedor");
                if (u2Local != null)
                {
                    u2Local.FullName = "Juan Vendedor Modificado Localmente";
                    u2Local.Email = "juan.local@empresa.com";
                    _db.SaveUser(u2Local);
                    
                    _db.EnqueueSyncItem(new SyncItem
                    {
                        TableName = "Users",
                        RecordId = "u2",
                        Operation = "UPDATE",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(u2Local)
                    });

                    RefreshOutboxCount();
                    
                    if (CurrentView is UserManagementViewModel uvm) uvm.LoadData();

                    MessageBox.Show("Conflicto forzado con éxito.\n\nEl usuario 'Juan Vendedor' (u2) tiene ahora cambios distintos en la Nube y en la BD Local.\n\nPresiona 'Sincronizar' en la barra superior para ver la resolución de conflictos interactiva.", "Conflicto Preparado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al preparar simulación: {ex.Message}");
            }
        }

        private string FormatJsonString(string json)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json;
            }
        }
    }
}
