using System.Windows;
using System.Collections.Generic;
using System.Linq;

namespace GUI
{
    public partial class DashboardCreationWindow : Window
    {
        public DashboardCreationWindow()
            : this(null, null)
        {
        }

        public DashboardCreationWindow(IEnumerable<MotorDashboardOption> motores, string motorSeleccionadoId)
        {
            InitializeComponent();
            ResponsiveWindowHelper.Ajustar(this, 900, 620);

            var listaMotores = (motores ?? new List<MotorDashboardOption>()).ToList();
            MotorComboBox.ItemsSource = listaMotores;
            if (listaMotores.Count > 0)
            {
                MotorComboBox.SelectedValue = string.IsNullOrWhiteSpace(motorSeleccionadoId)
                    ? listaMotores[0].Id
                    : motorSeleccionadoId;
            }
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
                MotorId = MotorComboBox.SelectedValue as string,
                MotorName = MotorComboBox.SelectedItem is MotorDashboardOption motor ? motor.Name : null,
                TemplateIndex = DashboardTemplateComboBox.SelectedIndex
            };

            DialogResult = true;
        }
    }
}
