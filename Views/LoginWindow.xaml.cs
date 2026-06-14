using System.Windows;

namespace InventarioApp.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            
            // Foco inicial en el cuadro de texto del usuario
            Loaded += (s, e) => TxtUser.Focus();
        }
    }
}
