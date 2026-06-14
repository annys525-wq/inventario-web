using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InventarioApp.Services;

namespace InventarioApp.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AuthService _auth;
        private string _username = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public event Action? LoginSuccess;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetProperty(ref _isBusy, value);
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }

        public bool IsNotBusy => !IsBusy;

        public ICommand LoginCommand { get; }

        public LoginViewModel(AuthService auth)
        {
            _auth = auth;
            LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(Username) && !IsBusy;
        }

        private void ExecuteLogin(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            if (passwordBox == null) return;

            string password = passwordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "La contraseña no puede estar vacía.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                bool result = _auth.Login(Username, password);
                if (result)
                {
                    LoginSuccess?.Invoke();
                }
                else
                {
                    ErrorMessage = "Credenciales incorrectas o usuario inactivo.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error al autenticar: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
