using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using InventarioApp.Models;
using InventarioApp.Services;

namespace InventarioApp.ViewModels
{
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private readonly AuthService _auth;
        private readonly AuditService _audit;

        private ObservableCollection<User> _usersList = new();
        private ObservableCollection<AuditLog> _auditLogsList = new();
        
        private User? _selectedUser;
        private string _usernameInput = string.Empty;
        private string _passwordInput = string.Empty;
        private string _fullNameInput = string.Empty;
        private string _emailInput = string.Empty;
        private UserRole _roleInput = UserRole.Vendedor;
        private bool _isActiveInput = true;
        private bool _hasAccess;
        private string _statusMessage = string.Empty;

        public ObservableCollection<User> UsersList
        {
            get => _usersList;
            set => SetProperty(ref _usersList, value);
        }

        public ObservableCollection<AuditLog> AuditLogsList
        {
            get => _auditLogsList;
            set => SetProperty(ref _auditLogsList, value);
        }

        public User? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value) && value != null)
                {
                    UsernameInput = value.Username;
                    FullNameInput = value.FullName;
                    EmailInput = value.Email;
                    RoleInput = value.Role;
                    IsActiveInput = value.IsActive;
                    PasswordInput = string.Empty; // No cargamos el hash
                }
            }
        }

        public string UsernameInput
        {
            get => _usernameInput;
            set => SetProperty(ref _usernameInput, value);
        }

        public string PasswordInput
        {
            get => _passwordInput;
            set => SetProperty(ref _passwordInput, value);
        }

        public string FullNameInput
        {
            get => _fullNameInput;
            set => SetProperty(ref _fullNameInput, value);
        }

        public string EmailInput
        {
            get => _emailInput;
            set => SetProperty(ref _emailInput, value);
        }

        public UserRole RoleInput
        {
            get => _roleInput;
            set => SetProperty(ref _roleInput, value);
        }

        public bool IsActiveInput
        {
            get => _isActiveInput;
            set => SetProperty(ref _isActiveInput, value);
        }

        public bool HasAccess
        {
            get => _hasAccess;
            set => SetProperty(ref _hasAccess, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand RefreshCommand { get; }

        public UserManagementViewModel(DatabaseService db, AuthService auth, AuditService audit)
        {
            _db = db;
            _auth = auth;
            _audit = audit;

            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            DeleteCommand = new RelayCommand(ExecuteDelete, CanExecuteDelete);
            ClearFormCommand = new RelayCommand(_ => ClearForm());
            RefreshCommand = new RelayCommand(_ => LoadData());

            CheckPermissionAndInitialize();
        }

        private void CheckPermissionAndInitialize()
        {
            if (_auth.CheckAuthorize("USER_CRUD"))
            {
                HasAccess = true;
                LoadData();
            }
            else
            {
                HasAccess = false;
                StatusMessage = "Acceso Denegado. Se requiere rol de Administrador.";
                
                // Auditar intento no autorizado
                string username = _auth.CurrentUser?.Username ?? "Desconocido";
                string userId = _auth.CurrentUser?.Id ?? "UNKNOWN";
                _audit.LogSecurityEvent(userId, username, "Access_Denied", "Intento de ingreso no autorizado al panel CRUD de Usuarios.");
            }
        }

        public void LoadData()
        {
            if (!HasAccess) return;

            try
            {
                UsersList = new ObservableCollection<User>(_db.GetUsers());
                AuditLogsList = new ObservableCollection<AuditLog>(_db.GetAuditLogs());
                StatusMessage = "Datos cargados correctamente.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar datos: {ex.Message}";
            }
        }

        private bool CanExecuteSave(object? parameter)
        {
            if (!HasAccess) return false;
            // Para usuarios nuevos se requiere contraseña
            if (SelectedUser == null && string.IsNullOrWhiteSpace(PasswordInput)) return false;
            return !string.IsNullOrWhiteSpace(UsernameInput) && 
                   !string.IsNullOrWhiteSpace(FullNameInput) && 
                   !string.IsNullOrWhiteSpace(EmailInput);
        }

        private void ExecuteSave(object? parameter)
        {
            try
            {
                var currentUser = _auth.CurrentUser;
                string currentUsername = currentUser?.Username ?? "System";
                string currentUserId = currentUser?.Id ?? "UNKNOWN";

                if (SelectedUser == null)
                {
                    // Crear nuevo usuario
                    var existing = _db.GetUserByUsername(UsernameInput);
                    if (existing != null)
                    {
                        MessageBox.Show("El nombre de usuario ya está registrado.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newUser = new User
                    {
                        Username = UsernameInput.Trim().ToLower(),
                        PasswordHash = AuthService.HashPassword(PasswordInput),
                        FullName = FullNameInput.Trim(),
                        Email = EmailInput.Trim(),
                        Role = RoleInput,
                        IsActive = IsActiveInput,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.SaveUser(newUser);
                    _audit.LogSecurityEvent(currentUserId, currentUsername, "User_Created", $"Usuario '{newUser.Username}' creado con rol {newUser.Role}.");
                    
                    // Agregar a la cola outbox
                    _db.EnqueueSyncItem(new SyncItem
                    {
                        TableName = "Users",
                        RecordId = newUser.Id,
                        Operation = "INSERT",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(newUser)
                    });
                }
                else
                {
                    // Editar usuario existente
                    SelectedUser.Username = UsernameInput.Trim().ToLower();
                    SelectedUser.FullName = FullNameInput.Trim();
                    SelectedUser.Email = EmailInput.Trim();
                    SelectedUser.Role = RoleInput;
                    SelectedUser.IsActive = IsActiveInput;

                    // Si escribió algo en el campo de contraseña, se actualiza la clave
                    if (!string.IsNullOrWhiteSpace(PasswordInput))
                    {
                        SelectedUser.PasswordHash = AuthService.HashPassword(PasswordInput);
                        _audit.LogSecurityEvent(currentUserId, currentUsername, "User_Password_Changed", $"Clave actualizada para '{SelectedUser.Username}'.");
                    }

                    _db.SaveUser(SelectedUser);
                    _audit.LogSecurityEvent(currentUserId, currentUsername, "User_Updated", $"Usuario '{SelectedUser.Username}' actualizado.");

                    // Registrar en cola de sincronización
                    _db.EnqueueSyncItem(new SyncItem
                    {
                        TableName = "Users",
                        RecordId = SelectedUser.Id,
                        Operation = "UPDATE",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(SelectedUser)
                    });
                }

                ClearForm();
                LoadData();
                MessageBox.Show("Usuario guardado con éxito y encolado para sincronización.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteDelete(object? parameter)
        {
            return HasAccess && SelectedUser != null;
        }

        private void ExecuteDelete(object? parameter)
        {
            if (SelectedUser == null) return;

            // Evitar eliminarse a sí mismo
            if (SelectedUser.Id == _auth.CurrentUser?.Id)
            {
                MessageBox.Show("No puedes eliminar al usuario activo con el que iniciaste sesión.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"¿Estás seguro de eliminar permanentemente al usuario '{SelectedUser.Username}'?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    string username = SelectedUser.Username;
                    string id = SelectedUser.Id;
                    
                    _db.DeleteUser(id);

                    var currentUser = _auth.CurrentUser;
                    _audit.LogSecurityEvent(currentUser?.Id ?? "UNKNOWN", currentUser?.Username ?? "System", "User_Deleted", $"Usuario '{username}' eliminado permanentemente.");

                    // Registrar en cola sync
                    _db.EnqueueSyncItem(new SyncItem
                    {
                        TableName = "Users",
                        RecordId = id,
                        Operation = "DELETE"
                    });

                    ClearForm();
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearForm()
        {
            SelectedUser = null;
            UsernameInput = string.Empty;
            PasswordInput = string.Empty;
            FullNameInput = string.Empty;
            EmailInput = string.Empty;
            RoleInput = UserRole.Vendedor;
            IsActiveInput = true;
        }
    }
}
