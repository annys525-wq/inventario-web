using System;
using System.Windows;
using InventarioApp.Services;
using InventarioApp.ViewModels;

namespace InventarioApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. Instanciar Servicios (IoC Contenedor Manual)
                var db = new DatabaseService();
                var audit = new AuditService();
                
                // Conectar audit logs a la base de datos
                audit.Configure(db);

                var firestore = new FirestoreService();
                var sync = new SyncEngine(db, firestore, audit);
                var auth = new AuthService(db, audit);

                // 2. Bloqueo de Inicio: Abrir Ventana de Login primero
                var loginWin = new Views.LoginWindow();
                var loginVm = new LoginViewModel(auth);

                loginVm.LoginSuccess += () =>
                {
                    // Al validar credenciales con éxito: inicializar ventana principal
                    var mainVm = new MainViewModel(db, auth, firestore, sync, audit);
                    var mainWin = new MainWindow();
                    mainWin.DataContext = mainVm;

                    this.MainWindow = mainWin;
                    mainWin.Show();
                    loginWin.Close();
                };

                loginWin.DataContext = loginVm;
                loginWin.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fallo crítico al iniciar la aplicación:\n\n{ex.Message}\n\nDetalles: {ex.InnerException?.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
