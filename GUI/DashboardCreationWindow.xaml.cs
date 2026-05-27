using System.Windows;

namespace GUI
{
    public partial class DashboardCreationWindow : Window
    {
        public DashboardCreationWindow()
        {
            InitializeComponent();
        }

        public DashboardStartupOptions SelectedOptions { get; private set; }

        private void CreateDashboardButton_Click(object sender, RoutedEventArgs e)
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

            DialogResult = true;
        }
    }
}
