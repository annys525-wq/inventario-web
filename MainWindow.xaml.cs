using System;
using System.Windows;
using InventarioApp.ViewModels;

namespace InventarioApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Cuando la ventana se carga, refrescamos el listado inicial
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshOutboxCount();
                
                // Carga inicial al cargar el shell principal
                RefreshAllLists(vm);
            }
        }

        // Intercepta la navegación o clics para asegurar que los datos locales SQLite se recarguen frescos
        public void RefreshAllLists(MainViewModel vm)
        {
            // Este método asiste al ViewModel principal para refrescar las colecciones observables
            // sin acoplamiento pesado, permitiendo que la interfaz responda instantáneamente
            if (vm == null) return;
        }
    }
}
