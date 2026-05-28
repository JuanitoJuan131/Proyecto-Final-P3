using BLL.Automation;
using ENTITY.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GUI
{
    public partial class AutomatizacionWindow : Window, INotifyPropertyChanged
    {
        private readonly int _idDashboard;
        private readonly AutomationRuleService _automationService = new AutomationRuleService();
        private AutomationRuleListItem _selectedRule;

        public AutomatizacionWindow(int idDashboard)
        {
            _idDashboard = idDashboard;
            InitializeComponent();
            ResponsiveWindowHelper.Ajustar(this, 900, 620);
            Rules = new ObservableCollection<AutomationRuleListItem>();
            DataContext = this;
            LoadTags();
            LoadRules();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<AutomationRuleListItem> Rules { get; private set; }

        public string DashboardLabel
        {
            get { return _idDashboard > 0 ? "#" + _idDashboard.ToString(CultureInfo.InvariantCulture) : "Sin guardar"; }
        }

        public string RuleCountText
        {
            get { return Rules == null ? "0" : Rules.Count.ToString(CultureInfo.InvariantCulture); }
        }

        public string ActiveRuleCountText
        {
            get
            {
                var count = Rules == null ? 0 : Rules.Count(r => r.Activa);
                return count.ToString(CultureInfo.InvariantCulture) + " activas";
            }
        }

        private void LoadTags()
        {
            var tags = _automationService.ObtenerTags()
                .Select(t => new TagOption(t))
                .OrderBy(t => t.Nombre)
                .ToList();

            TagComboBox.ItemsSource = tags;
            if (tags.Count > 0)
            {
                TagComboBox.SelectedIndex = 0;
            }
        }

        private void LoadRules()
        {
            Rules.Clear();
            foreach (var rule in _automationService.ObtenerResumenReglasDashboard(_idDashboard))
            {
                Rules.Add(new AutomationRuleListItem(rule));
            }

            RaisePropertyChanged("RuleCountText");
            RaisePropertyChanged("ActiveRuleCountText");
        }

        private void CreateRuleButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRule(false);
        }

        private void EditRuleButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRule(true);
        }

        private void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRule == null)
            {
                ShowNotification("Selecciona una regla para eliminar.", false);
                return;
            }

            try
            {
                _automationService.EliminarRegla(_selectedRule.IdRegla);
                ShowNotification("Regla eliminada correctamente.", true);
                ClearForm();
                LoadRules();
            }
            catch (Exception ex)
            {
                ShowNotification("No se pudo eliminar la regla: " + ex.Message, false);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            ShowNotification("Formulario listo para una nueva regla.", true);
        }

        private void RulesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedRule = RulesListBox.SelectedItem as AutomationRuleListItem;
            if (_selectedRule == null)
            {
                EditRuleButton.IsEnabled = false;
                DeleteRuleButton.IsEnabled = false;
                return;
            }

            RuleNameTextBox.Text = _selectedRule.Nombre;
            DescriptionTextBox.Text = _selectedRule.Descripcion;
            SelectTag(_selectedRule.IdTag);
            SelectComboValue(OperatorComboBox, _selectedRule.Operador);
            ThresholdTextBox.Text = _selectedRule.ValorUmbral.ToString("0.###", CultureInfo.InvariantCulture);
            SelectComboValue(SeverityComboBox, _selectedRule.Severidad);
            ActiveCheckBox.IsChecked = _selectedRule.Activa;
            EditRuleButton.IsEnabled = true;
            DeleteRuleButton.IsEnabled = true;
            StatusTextBlock.Text = "Editando regla: " + _selectedRule.Nombre;
        }

        private void SaveRule(bool edit)
        {
            if (_idDashboard <= 0)
            {
                ShowNotification("Guarda el dashboard antes de crear reglas.", false);
                return;
            }

            var selectedTag = TagComboBox.SelectedItem as TagOption;
            if (selectedTag == null)
            {
                ShowNotification("No hay tags disponibles. Espera a que el simulador registre sensores.", false);
                return;
            }

            double threshold;
            if (!double.TryParse(ThresholdTextBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out threshold))
            {
                ShowNotification("Valor umbral invalido.", false);
                return;
            }

            try
            {
                var definition = new AutomationRuleDefinition
                {
                    IdDashboard = _idDashboard,
                    Nombre = RuleNameTextBox.Text,
                    Descripcion = DescriptionTextBox.Text,
                    Activa = ActiveCheckBox.IsChecked == true,
                    IdTag = selectedTag.IdTag,
                    Operador = ((ComboBoxItem)OperatorComboBox.SelectedItem).Content.ToString(),
                    ValorUmbral = threshold,
                    TipoAccion = "MostrarPopup;GuardarEvento;CambiarWidget",
                    Severidad = ((ComboBoxItem)SeverityComboBox.SelectedItem).Content.ToString()
                };

                if (edit)
                {
                    if (_selectedRule == null)
                    {
                        ShowNotification("Selecciona una regla para editar.", false);
                        return;
                    }

                    _automationService.ActualizarRegla(_selectedRule.IdRegla, definition);
                    ShowNotification("Regla actualizada correctamente.", true);
                }
                else
                {
                    _automationService.CrearRegla(definition);
                    ShowNotification("Regla creada y condicion asociada guardada.", true);
                }

                LoadRules();
                ClearForm();
            }
            catch (Exception ex)
            {
                ShowNotification("No se pudo guardar la regla: " + ex.Message, false);
            }
        }

        private void ClearForm()
        {
            RulesListBox.SelectedItem = null;
            _selectedRule = null;
            RuleNameTextBox.Text = string.Empty;
            DescriptionTextBox.Text = string.Empty;
            ThresholdTextBox.Text = string.Empty;
            ActiveCheckBox.IsChecked = true;
            OperatorComboBox.SelectedIndex = 0;
            SeverityComboBox.SelectedIndex = 0;
            if (TagComboBox.Items.Count > 0)
            {
                TagComboBox.SelectedIndex = 0;
            }

            EditRuleButton.IsEnabled = false;
            DeleteRuleButton.IsEnabled = false;
            StatusTextBlock.Text = "Listo";
        }

        private void SelectTag(int idTag)
        {
            foreach (var item in TagComboBox.Items.OfType<TagOption>())
            {
                if (item.IdTag == idTag)
                {
                    TagComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static void SelectComboValue(ComboBox comboBox, string value)
        {
            foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(Convert.ToString(item.Content, CultureInfo.InvariantCulture), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void ShowNotification(string message, bool success)
        {
            StatusTextBlock.Text = message;
            NotificationTextBlock.Text = message;
            NotificationTextBlock.Foreground = success
                ? new SolidColorBrush(Color.FromRgb(223, 252, 246))
                : new SolidColorBrush(Color.FromRgb(254, 226, 226));
            NotificationBorder.Background = success
                ? new SolidColorBrush(Color.FromRgb(18, 59, 54))
                : new SolidColorBrush(Color.FromRgb(50, 24, 32));
            NotificationBorder.BorderBrush = success
                ? new SolidColorBrush(Color.FromRgb(40, 199, 164))
                : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            NotificationBorder.Visibility = Visibility.Visible;
            NotificationBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(success ? 3 : 5) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
                fade.Completed += (s, e) => NotificationBorder.Visibility = Visibility.Collapsed;
                NotificationBorder.BeginAnimation(OpacityProperty, fade);
            };
            timer.Start();
        }

        private void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class TagOption
    {
        public TagOption(TagDato tag)
        {
            IdTag = tag.IdTag;
            Nombre = tag.Nombre;
            Unidad = tag.Unidad;
        }

        public int IdTag { get; set; }
        public string Nombre { get; set; }
        public string Unidad { get; set; }

        public string DisplayName
        {
            get { return Nombre + (string.IsNullOrWhiteSpace(Unidad) ? string.Empty : " (" + Unidad + ")"); }
        }
    }

    public class AutomationRuleListItem
    {
        public AutomationRuleListItem(AutomationRuleSummary rule)
        {
            IdRegla = rule.IdRegla;
            Nombre = rule.Nombre;
            Descripcion = rule.Descripcion;
            Activa = rule.Activa;
            IdTag = rule.IdTag;
            Sensor = rule.Sensor;
            Unidad = rule.Unidad;
            Operador = rule.Operador;
            ValorUmbral = rule.ValorUmbral;
            Severidad = rule.Severidad;
            Color = rule.Color;
        }

        public int IdRegla { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public bool Activa { get; set; }
        public int IdTag { get; set; }
        public string Sensor { get; set; }
        public string Unidad { get; set; }
        public string Operador { get; set; }
        public double ValorUmbral { get; set; }
        public string Severidad { get; set; }
        public string Color { get; set; }

        public string EstadoTexto
        {
            get { return Activa ? "ACTIVA" : "INACTIVA"; }
        }

        public string SensorText
        {
            get { return "TAG  " + Sensor + (string.IsNullOrWhiteSpace(Unidad) ? string.Empty : " (" + Unidad + ")"); }
        }

        public string ConditionText
        {
            get { return Operador + " " + ValorUmbral.ToString("0.###", CultureInfo.InvariantCulture); }
        }

        public string SeverityIcon
        {
            get
            {
                if (string.Equals(Severidad, "Critica", StringComparison.OrdinalIgnoreCase)) return "HI";
                if (string.Equals(Severidad, "Advertencia", StringComparison.OrdinalIgnoreCase)) return "WR";
                return "OK";
            }
        }

        public Brush SeverityBrush
        {
            get { return new SolidColorBrush(ParseColor(Color, System.Windows.Media.Color.FromRgb(34, 197, 94))); }
        }

        public Brush SeverityBadgeBrush
        {
            get
            {
                var color = ParseColor(Color, System.Windows.Media.Color.FromRgb(34, 197, 94));
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(38, color.R, color.G, color.B));
            }
        }

        public Brush StatusBrush
        {
            get { return Activa ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 199, 164)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 182, 199)); }
        }

        private static System.Windows.Media.Color ParseColor(string color, System.Windows.Media.Color fallback)
        {
            try
            {
                return string.IsNullOrWhiteSpace(color) ? fallback : (Color)ColorConverter.ConvertFromString(color);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
