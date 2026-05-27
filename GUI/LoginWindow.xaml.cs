using System.Windows;

namespace GUI
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        public DashboardStartupOptions SelectedOptions { get; private set; }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOptions = new DashboardStartupOptions
            {
                ProjectName = string.IsNullOrWhiteSpace(ProjectNameTextBox.Text)
                    ? "Proyecto VisualIoT"
                    : ProjectNameTextBox.Text.Trim(),
                DashboardName = string.IsNullOrWhiteSpace(DashboardNameTextBox.Text)
                    ? "Dashboard principal"
                    : DashboardNameTextBox.Text.Trim(),
                TemplateIndex = DashboardTemplateComboBox.SelectedIndex
            };

            var dashboard = new MainWindow(SelectedOptions);
            dashboard.Show();
            Close();
        }
    }
}
