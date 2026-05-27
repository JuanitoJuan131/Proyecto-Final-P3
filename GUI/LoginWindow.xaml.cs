using BLL.Security;
using System.Windows;

namespace GUI
{
    public partial class LoginWindow : Window
    {
        private readonly AutenticacionService _autenticacionService = new AutenticacionService();

        public LoginWindow()
        {
            InitializeComponent();
            ResponsiveWindowHelper.Ajustar(this, 900, 620);
        }

        public DashboardStartupOptions SelectedOptions { get; private set; }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = false;
            LoginStatusTextBlock.Text = "Validando usuario...";

            var resultado = _autenticacionService.Autenticar(EmailTextBox.Text, PasswordBox.Password);
            if (!resultado.Autenticado)
            {
                LoginStatusTextBlock.Text = resultado.Mensaje;
                LoginButton.IsEnabled = true;
                return;
            }

            SelectedOptions = new DashboardStartupOptions
            {
                ProjectName = "Proyecto VisualIoT",
                DashboardName = "Dashboard principal",
                TemplateIndex = 0
            };

            var dashboard = new MainWindow(SelectedOptions);
            dashboard.Show();
            Close();
        }
    }
}
