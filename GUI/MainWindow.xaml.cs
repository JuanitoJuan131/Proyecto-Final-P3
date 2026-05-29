using BLL.Automation;
using BLL.Dashboards;
using BLL.Simulation;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GUI
{
    public partial class MainWindow : Window
    {
        private const int CellSize = 88;
        private readonly AutomationRuleService _automationService = new AutomationRuleService();
        private readonly DashboardWorkspaceService _dashboardService = new DashboardWorkspaceService();
        private readonly SimulationManager _simulationManager = new SimulationManager();
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
        private readonly Dictionary<string, MotorCardViewModel> _motorMap = new Dictionary<string, MotorCardViewModel>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TagRowViewModel> _tagRows = new Dictionary<string, TagRowViewModel>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastAutomationEvaluation = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<DashboardWidgetLayout>> _motorLayouts = new Dictionary<string, List<DashboardWidgetLayout>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ObservableCollection<AlarmEventViewModel>> _alarmHistoryByMotor = new Dictionary<string, ObservableCollection<AlarmEventViewModel>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DashboardWidgetViewModel> _widgets = new List<DashboardWidgetViewModel>();
        private DashboardWidgetViewModel _draggingWidget;
        private DashboardWidgetViewModel _selectedWidget;
        private Point _dragOffset;
        private bool _wasDragged;
        private bool _simulationRunning = true;
        private bool _autoShutdownOnCritical = true;
        private bool _mqttReconnectPending;
        private string _lastMqttServer;
        private int _lastMqttPort;
        private string _lastMqttTopic;
        private int _mqttMessageCount;
        private DashboardStartupOptions _startupOptions;
        private string _selectedMotorId;
        private int _currentProjectId;
        private int _currentDashboardId;

        public MainWindow()
            : this(new DashboardStartupOptions())
        {
        }

        public MainWindow(DashboardStartupOptions startupOptions)
        {
            _startupOptions = startupOptions ?? new DashboardStartupOptions();
            InitializeComponent();
            ResponsiveWindowHelper.Ajustar(this, 1360, 820);
            WidgetPaletteItems = new ObservableCollection<WidgetPaletteItem>();
            Motors = new ObservableCollection<MotorCardViewModel>();
            Tags = new ObservableCollection<TagRowViewModel>();
            Events = new ObservableCollection<string>();

            LoadPalette();
            LoadMotors();
            DataContext = this;
            SetSelectedMotor(string.IsNullOrWhiteSpace(_startupOptions.MotorId) ? "MOTOR_01" : _startupOptions.MotorId);
            _currentProjectId = _startupOptions.ProjectId;
            _currentDashboardId = _startupOptions.DashboardId;
            CreateDashboardFromOptions(_startupOptions);
        }

        public ObservableCollection<WidgetPaletteItem> WidgetPaletteItems { get; private set; }
        public ObservableCollection<MotorCardViewModel> Motors { get; private set; }
        public ObservableCollection<TagRowViewModel> Tags { get; private set; }
        public ObservableCollection<string> Events { get; private set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _simulationManager.TagValueChanged += SimulationManager_TagValueChanged;
            _simulationManager.MqttConnectionChanged += SimulationManager_MqttConnectionChanged;
            _simulationManager.MqttMessageReceived += SimulationManager_MqttMessageReceived;
            _simulationManager.PersistenceWarning += SimulationManager_PersistenceWarning;
            _simulationManager.TagsCleared += SimulationManager_TagsCleared;
            _simulationManager.StartSimulation();

            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, args) =>
                StatusBarTextBlock.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                    + "  |  " + Tags.Count + " tags activos"
                    + "  |  MQTT mensajes: " + _mqttMessageCount;
            _clockTimer.Start();
            AddEvent("Simulacion industrial iniciada");
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            _simulationManager.StopSimulation();
            await _simulationManager.Mqtt.DisconnectAsync();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectMqttAsync();
        }

        private async Task ConnectMqttAsync()
        {
            int port;
            if (!int.TryParse(PortTextBox.Text, out port))
            {
                StatusBarTextBlock.Text = "Puerto MQTT invalido";
                return;
            }

            var wasSimulationRunning = _simulationRunning;

            try
            {
                ConnectButton.IsEnabled = false;
                StatusBarTextBlock.Text = "Conectando a MQTT...";

                if (_simulationRunning)
                {
                    _simulationManager.StopSimulation();
                    _simulationRunning = false;
                    SimulationButton.Content = "Iniciar simulacion";
                }

                _simulationManager.UseMqttTelemetry(true);
                _simulationManager.ClearTags();

                _lastMqttServer = BrokerTextBox.Text.Trim();
                _lastMqttPort = port;
                _lastMqttTopic = TopicTextBox.Text.Trim();

                await _simulationManager.ConnectMqttAsync(_lastMqttServer, _lastMqttPort, _lastMqttTopic);
                if (wasSimulationRunning)
                {
                    AddEvent("Simulacion local pausada: usando telemetria MQTT del ESP32");
                }

                StatusBarTextBlock.Text = "Suscrito a " + _lastMqttTopic;
                AddEvent("MQTT conectado a " + _lastMqttServer);
                AddEvent("Esperando datos del ESP32 en " + _lastMqttTopic);
            }
            catch (Exception ex)
            {
                _simulationManager.UseMqttTelemetry(false);
                if (wasSimulationRunning)
                {
                    _simulationManager.StartSimulation();
                    _simulationRunning = true;
                    SimulationButton.Content = "Detener simulacion";
                }

                StatusBarTextBlock.Text = "No se pudo conectar MQTT: " + ex.Message;
                AddEvent("Error MQTT: " + ex.Message);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && _selectedWidget != null)
            {
                RemoveWidget(_selectedWidget);
                e.Handled = true;
            }
        }

        private void SimulationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_simulationRunning)
            {
                _simulationManager.StopSimulation();
                _simulationRunning = false;
                SimulationButton.Content = "Iniciar simulacion";
                AddEvent("Simulacion detenida");
            }
            else
            {
                _simulationManager.UseMqttTelemetry(false);
                _simulationManager.StartSimulation();
                _simulationRunning = true;
                SimulationButton.Content = "Detener simulacion";
                AddEvent("Simulacion reanudada");
            }
        }

        private void ShutdownMotorButton_Click(object sender, RoutedEventArgs e)
        {
            ShutdownMotor(_selectedMotorId, "Apagado manual desde SCADA");
        }

        private void RestartMotorButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedMotorId))
            {
                return;
            }

            if (!_simulationManager.MqttTelemetryActive && !_simulationRunning)
            {
                _simulationManager.StartSimulation();
                _simulationRunning = true;
                SimulationButton.Content = "Detener simulacion";
            }

            _simulationManager.RestartMotor(_selectedMotorId);
            SetMotorRuntimeState(_selectedMotorId, "Arrancando", "Reinicio solicitado");
            RegisterAlarm(_selectedMotorId + ".Alarma", "Reinicio manual del motor", "Normal", false);
            AddEvent("Reinicio solicitado para " + _selectedMotorId);
        }

        private async void StopMotorInputButton_Click(object sender, RoutedEventArgs e)
        {
            if (_simulationManager.MqttTelemetryActive)
            {
                await _simulationManager.DisconnectMqttAsync();
                ConnectionStatusTextBlock.Text = "MQTT desconectado";
                ConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(182, 193, 208));
                AddEvent("Recepcion MQTT desconectada para control seguro");
                return;
            }

            _simulationManager.StopSimulation();
            _simulationRunning = false;
            SimulationButton.Content = "Iniciar simulacion";
            AddEvent("Simulacion detenida desde control de motor");
        }

        private void NewDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            var setupWindow = new DashboardCreationWindow(GetMotorDashboardOptions(), _selectedMotorId)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (setupWindow.ShowDialog() == true)
            {
                setupWindow.SelectedOptions.UserId = _startupOptions.UserId;
                setupWindow.SelectedOptions.UserName = _startupOptions.UserName;
                setupWindow.SelectedOptions.ProjectId = _currentProjectId;
                CreateDashboardFromOptions(setupWindow.SelectedOptions);
            }
        }

        private void SaveDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            GuardarDashboardActual();
        }

        private void AutomationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDashboardId <= 0)
            {
                GuardarDashboardActual();
            }

            if (_currentDashboardId <= 0)
            {
                AddEvent("Guarda el dashboard antes de configurar automatizacion.");
                return;
            }

            var window = new AutomatizacionWindow(_currentDashboardId)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private void BackToProjectsButton_Click(object sender, RoutedEventArgs e)
        {
            var usuario = new Usuario
            {
                IdUsuario = _startupOptions.UserId,
                Nombre = string.IsNullOrWhiteSpace(_startupOptions.UserName) ? "Usuario" : _startupOptions.UserName,
                Rol = string.IsNullOrWhiteSpace(_startupOptions.UserRole) ? "operario" : _startupOptions.UserRole
            };

            var projectsWindow = new ProyectosRecientes(usuario);
            projectsWindow.Show();
            Close();
        }

        private void WidgetPalette_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var item = WidgetPalette.SelectedItem as WidgetPaletteItem;
            if (item == null)
            {
                return;
            }

            DragDrop.DoDragDrop(WidgetPalette, item.Type.ToString(), DragDropEffects.Copy);
        }

        private void DashboardCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DashboardCanvas_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                return;
            }

            var typeName = e.Data.GetData(DataFormats.StringFormat) as string;
            TipoWidget tipoWidget;
            if (!Enum.TryParse(typeName, out tipoWidget))
            {
                return;
            }

            var position = e.GetPosition(DashboardCanvas);
            AddWidget(tipoWidget, Snap(position.X), Snap(position.Y));
        }

        private void MotorCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            var motorId = border != null ? border.Tag as string : null;
            if (!string.IsNullOrWhiteSpace(motorId))
            {
                SwitchMotorTab(motorId);
            }
        }

        private void DashboardCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingWidget == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var point = e.GetPosition(DashboardCanvas);
            var left = Clamp(point.X - _dragOffset.X, 0, Math.Max(0, DashboardCanvas.ActualWidth - _draggingWidget.Container.ActualWidth));
            var top = Clamp(point.Y - _dragOffset.Y, 0, Math.Max(0, DashboardCanvas.ActualHeight - _draggingWidget.Container.ActualHeight));
            Canvas.SetLeft(_draggingWidget.Container, left);
            Canvas.SetTop(_draggingWidget.Container, top);
            _wasDragged = true;
        }

        private void DashboardCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingWidget == null)
            {
                return;
            }

            _draggingWidget.Container.ReleaseMouseCapture();
            _draggingWidget.Container.Opacity = 1;
            Panel.SetZIndex(_draggingWidget.Container, 0);

            if (_wasDragged)
            {
                var left = Snap(Canvas.GetLeft(_draggingWidget.Container));
                var top = Snap(Canvas.GetTop(_draggingWidget.Container));
                AnimateWidgetTo(_draggingWidget.Container, left, top);
            }

            SelectWidget(_draggingWidget);
            _draggingWidget = null;
        }

        private static void AnimateWidgetTo(UIElement element, double left, double top)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var currentLeft = Canvas.GetLeft(element);
            var currentTop = Canvas.GetTop(element);

            var leftAnimation = new DoubleAnimation(currentLeft, left, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
            var topAnimation = new DoubleAnimation(currentTop, top, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };

            leftAnimation.Completed += (sender, args) =>
            {
                element.BeginAnimation(Canvas.LeftProperty, null);
                element.BeginAnimation(Canvas.TopProperty, null);
                Canvas.SetLeft(element, left);
                Canvas.SetTop(element, top);
            };

            element.BeginAnimation(Canvas.LeftProperty, leftAnimation);
            element.BeginAnimation(Canvas.TopProperty, topAnimation);
        }

        private void SimulationManager_MqttConnectionChanged(bool connected, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionStatusTextBlock.Text = connected ? "MQTT conectado" : "MQTT desconectado";
                ConnectionStatusTextBlock.Foreground = connected
                    ? new SolidColorBrush(Color.FromRgb(40, 199, 164))
                    : new SolidColorBrush(Color.FromRgb(143, 160, 179));

                if (connected)
                {
                    _mqttReconnectPending = false;
                }
                else if (_simulationManager.MqttTelemetryActive && !_mqttReconnectPending)
                {
                    ScheduleMqttReconnect();
                }
            });
        }

        private void ScheduleMqttReconnect()
        {
            if (string.IsNullOrWhiteSpace(_lastMqttServer) || string.IsNullOrWhiteSpace(_lastMqttTopic))
            {
                return;
            }

            _mqttReconnectPending = true;
            AddEvent("MQTT desconectado: reconexion automatica en 5 s");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += async (sender, args) =>
            {
                timer.Stop();
                try
                {
                    await _simulationManager.ConnectMqttAsync(_lastMqttServer, _lastMqttPort, _lastMqttTopic);
                    _mqttReconnectPending = false;
                    AddEvent("MQTT reconectado automaticamente");
                }
                catch (Exception ex)
                {
                    _mqttReconnectPending = false;
                    AddEvent("Fallo de reconexion MQTT: " + ex.Message);
                    if (_simulationManager.MqttTelemetryActive)
                    {
                        ScheduleMqttReconnect();
                    }
                }
            };
            timer.Start();
        }

        private void SimulationManager_MqttMessageReceived(string topic, string payload)
        {
            Dispatcher.Invoke(() =>
            {
                _mqttMessageCount++;
                if (_mqttMessageCount <= 5 || _mqttMessageCount % 10 == 0)
                {
                    AddEvent("MQTT <- " + topic + " = " + payload);
                }
            });
        }

        private void SimulationManager_PersistenceWarning(string message)
        {
            Dispatcher.Invoke(() => AddEvent(message));
        }

        private void SimulationManager_TagValueChanged(string tag, object value)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateMotorCards(tag, value);

                if (!IsSelectedMotorTag(tag))
                {
                    return;
                }

                UpdateTagGrid(tag, value);
                UpdateDashboardWidgets(tag, value);
                EvaluateAutomation(tag, value);
            });
        }

        private void EvaluateAutomation(string tag, object value)
        {
            double number;
            if (_currentDashboardId <= 0 || !double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
            {
                return;
            }

            DateTime lastEvaluation;
            if (_lastAutomationEvaluation.TryGetValue(tag, out lastEvaluation) && DateTime.Now - lastEvaluation < TimeSpan.FromSeconds(1))
            {
                return;
            }
            _lastAutomationEvaluation[tag] = DateTime.Now;

            try
            {
                foreach (var result in _automationService.Evaluar(_currentDashboardId, tag, number))
                {
                    AddEvent("Automatizacion: " + result.Description);
                    RegisterAlarm(result.Tag, result.Description, result.Severity, true);
                    ApplyAutomationVisualState(result);
                    ShowAutomationAlert(result);
                }
            }
            catch (Exception ex)
            {
                AddEvent("Error de automatizacion: " + ex.Message);
            }
        }

        private void ApplyAutomationVisualState(AutomationTriggerResult result)
        {
            var color = ParseColor(result.Color) ?? Color.FromRgb(239, 68, 68);
            foreach (var widget in _widgets.Where(w => TagsMatch(w.Tag, result.Tag)))
            {
                if (widget.Container == null)
                {
                    continue;
                }

                widget.Container.BorderBrush = new SolidColorBrush(color);
                widget.Container.BorderThickness = new Thickness(2);
                widget.Container.Background = new SolidColorBrush(Color.FromArgb(68, color.R, color.G, color.B));
            }
        }

        private void ShowAutomationAlert(AutomationTriggerResult result)
        {
            var color = ParseColor(result.Color) ?? Color.FromRgb(239, 68, 68);
            AlertPopup.Visibility = Visibility.Visible;
            AlertPopup.Background = new SolidColorBrush(Color.FromArgb(238, 30, 20, 24));
            AlertPopup.BorderBrush = new SolidColorBrush(color);
            AlertTitleTextBlock.Text = result.Severity.ToUpperInvariant() + " - " + result.RuleName;
            AlertMessageTextBlock.Text = result.Description;

            AlertPopup.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
                fade.Completed += (s, e) => AlertPopup.Visibility = Visibility.Collapsed;
                AlertPopup.BeginAnimation(OpacityProperty, fade);
            };
            timer.Start();
        }

        private void RegisterAlarm(string tag, string message, string severity, bool notify)
        {
            if (string.IsNullOrWhiteSpace(message) || string.Equals(message, "Sin alarmas", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var motorId = MotorIdFromTag(tag);
            if (string.IsNullOrWhiteSpace(motorId))
            {
                motorId = _selectedMotorId ?? "MOTOR_01";
            }

            var alarm = new AlarmEventViewModel
            {
                Time = DateTime.Now,
                MotorId = motorId,
                Severity = string.IsNullOrWhiteSpace(severity) ? "Normal" : severity,
                Message = message,
                Tag = tag,
                ColorBrush = new SolidColorBrush(ColorForSeverity(severity)),
                Icon = IconForSeverity(severity)
            };

            var history = GetAlarmHistory(motorId);
            if (history.Count > 0 && history[0].Message == alarm.Message && DateTime.Now - history[0].Time < TimeSpan.FromSeconds(2))
            {
                return;
            }

            history.Insert(0, alarm);
            if (history.Count > 50)
            {
                history.RemoveAt(history.Count - 1);
            }

            foreach (var widget in _widgets.Where(w => w.Type == TipoWidget.PanelAlarmas && string.Equals(MotorIdFromTag(w.Tag), motorId, StringComparison.OrdinalIgnoreCase)))
            {
                widget.AlarmItems = history;
                RenderAlarmList(widget);
                if (widget.ValueBlock != null)
                {
                    widget.ValueBlock.Text = alarm.Severity.ToUpperInvariant() + " - " + alarm.Message;
                    widget.ValueBlock.Foreground = alarm.ColorBrush;
                }
            }

            if (notify && string.Equals(motorId, _selectedMotorId, StringComparison.OrdinalIgnoreCase))
            {
                AlertPopup.Visibility = Visibility.Visible;
                AlertPopup.BorderBrush = alarm.ColorBrush;
                AlertTitleTextBlock.Text = alarm.Severity.ToUpperInvariant() + " - " + motorId;
                AlertMessageTextBlock.Text = alarm.Message;
                AlertPopup.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }

            if (IsCriticalSeverity(severity) && _autoShutdownOnCritical)
            {
                ShutdownMotor(motorId, "Paro automatico por alarma critica");
            }
        }

        private ObservableCollection<AlarmEventViewModel> GetAlarmHistory(string motorId)
        {
            motorId = string.IsNullOrWhiteSpace(motorId) ? (_selectedMotorId ?? "MOTOR_01") : motorId;
            ObservableCollection<AlarmEventViewModel> history;
            if (!_alarmHistoryByMotor.TryGetValue(motorId, out history))
            {
                history = new ObservableCollection<AlarmEventViewModel>();
                _alarmHistoryByMotor[motorId] = history;
            }

            return history;
        }

        private static void RenderAlarmList(DashboardWidgetViewModel widget)
        {
            if (widget == null || widget.AlarmList == null)
            {
                return;
            }

            widget.AlarmList.Items.Clear();
            var items = widget.AlarmItems ?? new ObservableCollection<AlarmEventViewModel>();
            if (items.Count == 0)
            {
                widget.AlarmList.Items.Add(new TextBlock
                {
                    Text = "Sin alarmas activas",
                    Foreground = new SolidColorBrush(Color.FromRgb(45, 212, 191)),
                    Margin = new Thickness(6)
                });
                return;
            }

            foreach (var alarm in items.Take(8))
            {
                var row = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(10, 20, 32)),
                    BorderBrush = alarm.ColorBrush,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                row.Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = alarm.Icon + " " + alarm.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + alarm.Severity.ToUpperInvariant(),
                            Foreground = alarm.ColorBrush,
                            FontSize = 10.5,
                            FontWeight = FontWeights.Bold
                        },
                        new TextBlock
                        {
                            Text = alarm.Message,
                            Foreground = new SolidColorBrush(Color.FromRgb(222, 234, 245)),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 3, 0, 0)
                        }
                    }
                };
                widget.AlarmList.Items.Add(row);
            }
        }

        private void ShutdownMotor(string motorId, string reason)
        {
            if (string.IsNullOrWhiteSpace(motorId))
            {
                return;
            }

            if (_simulationManager.MqttTelemetryActive)
            {
                _simulationManager.DisconnectMqttAsync();
                ConnectionStatusTextBlock.Text = "MQTT desconectado";
                ConnectionStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(182, 193, 208));
            }
            else if (_simulationRunning)
            {
                _simulationManager.StopSimulation();
                _simulationRunning = false;
                SimulationButton.Content = "Iniciar simulacion";
            }

            _simulationManager.ShutdownMotor(motorId);
            SetMotorRuntimeState(motorId, "Apagado", reason);
            AddEvent(reason + ": " + motorId);
        }

        private void SetMotorRuntimeState(string motorId, string state, string detail)
        {
            MotorCardViewModel motor;
            if (_motorMap.TryGetValue(motorId, out motor))
            {
                motor.StateText = state;
                motor.RpmText = state == "Apagado" ? "0.0" : motor.RpmText;
            }

            foreach (var widget in _widgets.Where(w => string.Equals(MotorIdFromTag(w.Tag), motorId, StringComparison.OrdinalIgnoreCase)))
            {
                if (widget.Container != null)
                {
                    widget.Container.BorderBrush = state == "Apagado"
                        ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(40, 199, 164));
                }
            }

            StatusBarTextBlock.Text = detail + " - " + motorId;
        }

        private void SimulationManager_TagsCleared()
        {
            Dispatcher.Invoke(() =>
            {
                _tagRows.Clear();
                Tags.Clear();
                ResetDashboardWidgets();
            });
        }

        private void LoadPalette()
        {
            WidgetPaletteItems.Add(new WidgetPaletteItem(TipoWidget.Medidor, "Gauge", "RPM, presion o vibracion"));
            WidgetPaletteItems.Add(new WidgetPaletteItem(TipoWidget.Numerico, "Numerico", "Valor puntual de cualquier tag"));
            WidgetPaletteItems.Add(new WidgetPaletteItem(TipoWidget.Tanque, "Tanque", "Nivel de pulpa o jugo"));
            WidgetPaletteItems.Add(new WidgetPaletteItem(TipoWidget.Tendencia, "Tendencia", "Lecturas recientes"));
            WidgetPaletteItems.Add(new WidgetPaletteItem(TipoWidget.Led, "Led", "Estado discreto o alarma"));
            WidgetPaletteItems.Add(new WidgetPaletteItem(TipoWidget.Motor, "Motor", "Resumen de un equipo"));
            WidgetPaletteItems.Add(new WidgetPaletteItem(TipoWidget.PanelAlarmas, "Alarmas", "Mensajes activos"));
        }

        private void CreateDashboardFromOptions(DashboardStartupOptions options)
        {
            options = options ?? new DashboardStartupOptions();
            _startupOptions = options;

            var projectName = string.IsNullOrWhiteSpace(options.ProjectName)
                ? "Proyecto VisualIoT"
                : options.ProjectName.Trim();
            var dashboardName = string.IsNullOrWhiteSpace(options.DashboardName)
                ? "Dashboard principal"
                : options.DashboardName.Trim();
            var motorId = string.IsNullOrWhiteSpace(options.MotorId)
                ? (_selectedMotorId ?? "MOTOR_01")
                : options.MotorId.Trim();

            SetSelectedMotor(motorId);
            _currentProjectId = options.ProjectId;
            _currentDashboardId = options.DashboardId;
            ProjectNameHeaderTextBlock.Text = projectName;
            DashboardNameHeaderTextBlock.Text = dashboardName + " - " + motorId;

            DashboardCanvas.Children.Clear();
            _widgets.Clear();
            EmptyDashboardText.Visibility = Visibility.Visible;

            if (options.LoadFromLayout && !string.IsNullOrWhiteSpace(options.LayoutJson))
            {
                LoadDashboardFromJson(options.LayoutJson);
                AddEvent("Dashboard recuperado desde Oracle: " + dashboardName);
                return;
            }

            var template = options.TemplateIndex;
            if (template == 0)
            {
                AddDefaultWidgets(motorId);
            }
            else if (template == 2)
            {
                AddWidget(TipoWidget.Medidor, 0, 0, motorId + ".RPM", "Velocidad");
                AddWidget(TipoWidget.Numerico, 264, 0, motorId + ".Temperatura", "Temperatura");
                AddWidget(TipoWidget.Numerico, 528, 0, motorId + ".Corriente", "Corriente");
                AddWidget(TipoWidget.Tendencia, 0, 264, motorId + ".RPM", "Historico en tiempo real");
                AddWidget(TipoWidget.PanelAlarmas, 352, 264, motorId + ".Alarma", "Registro de alertas");
            }

            AddEvent("Dashboard creado: " + dashboardName + " para " + motorId);
        }

        private void LoadMotors()
        {
            foreach (var motor in _simulationManager.Engine.Motors)
            {
                var viewModel = new MotorCardViewModel
                {
                    Id = motor.Id,
                    Name = motor.Nombre,
                    Line = motor.LineaProduccion,
                    StateText = "Apagado",
                    Efficiency = motor.Eficiencia
                };
                _motorMap[motor.Id] = viewModel;
                Motors.Add(viewModel);
            }
        }

        private IEnumerable<MotorDashboardOption> GetMotorDashboardOptions()
        {
            return Motors.Select(m => new MotorDashboardOption { Id = m.Id, Name = m.Name }).ToList();
        }

        private void SetSelectedMotor(string motorId)
        {
            if (string.IsNullOrWhiteSpace(motorId) && Motors.Count > 0)
            {
                motorId = Motors[0].Id;
            }

            _selectedMotorId = motorId;
            foreach (var motor in Motors)
            {
                motor.IsSelected = string.Equals(motor.Id, _selectedMotorId, StringComparison.OrdinalIgnoreCase);
            }

            MotorCardViewModel selected;
            if (_motorMap.TryGetValue(_selectedMotorId ?? string.Empty, out selected))
            {
                SelectedMotorHeaderTextBlock.Text = "Motor: " + selected.Id + " - " + selected.Name;
                _startupOptions.MotorId = selected.Id;
                _startupOptions.MotorName = selected.Name;
            }
        }

        private void SwitchMotorTab(string motorId)
        {
            if (string.IsNullOrWhiteSpace(motorId)
                || string.Equals(motorId, _selectedMotorId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SaveCurrentMotorLayout();
            SetSelectedMotor(motorId);
            LoadMotorWorkspace(motorId);
            RefreshSelectedMotorTags();
            AddEvent("Pestana de motor activa: " + motorId);
        }

        private void SaveCurrentMotorLayout()
        {
            if (string.IsNullOrWhiteSpace(_selectedMotorId))
            {
                return;
            }

            _motorLayouts[_selectedMotorId] = _widgets.Select(w =>
            {
                var left = w.Container == null ? 0 : Canvas.GetLeft(w.Container);
                var top = w.Container == null ? 0 : Canvas.GetTop(w.Container);
                return new DashboardWidgetLayout
                {
                    tipo = w.Type.ToString(),
                    x = double.IsNaN(left) ? 0 : left,
                    y = double.IsNaN(top) ? 0 : top,
                    width = w.Container == null ? 0 : w.Container.Width,
                    height = w.Container == null ? 0 : w.Container.Height,
                    tag = w.Tag,
                    sensor = w.Tag,
                    titulo = w.Title,
                    color = w.Color
                };
            }).ToList();
        }

        private void LoadMotorWorkspace(string motorId)
        {
            DashboardCanvas.Children.Clear();
            _widgets.Clear();
            _selectedWidget = null;
            EmptyDashboardText.Visibility = Visibility.Visible;

            List<DashboardWidgetLayout> saved;
            if (_motorLayouts.TryGetValue(motorId, out saved) && saved.Count > 0)
            {
                foreach (var item in saved)
                {
                    AddWidget(
                        WidgetTypeFromLayout(item.tipo),
                        item.x,
                        item.y,
                        string.IsNullOrWhiteSpace(item.tag) ? item.sensor : item.tag,
                        string.IsNullOrWhiteSpace(item.titulo) ? DefaultTitleFor(WidgetTypeFromLayout(item.tipo)) : item.titulo,
                        item.width > 0 ? item.width : CellSize * 3,
                        item.height > 0 ? item.height : CellSize * 2,
                        item.color);
                }
            }
            else
            {
                AddDefaultWidgets(motorId);
            }

            foreach (var widget in _widgets)
            {
                ApplyCurrentValue(widget);
            }
        }

        private void RefreshSelectedMotorTags()
        {
            _tagRows.Clear();
            Tags.Clear();

            foreach (var tag in _simulationManager.Tags)
            {
                if (IsSelectedMotorTag(tag.Key))
                {
                    UpdateTagGrid(tag.Key, tag.Value);
                }
            }
        }

        private void AddDefaultWidgets(string motorId)
        {
            motorId = string.IsNullOrWhiteSpace(motorId) ? "MOTOR_01" : motorId;
            AddWidget(TipoWidget.Medidor, 0, 0, motorId + ".RPM", "Velocidad RPM");
            AddWidget(TipoWidget.Numerico, 264, 0, motorId + ".Temperatura", "Temperatura");
            AddWidget(TipoWidget.Tanque, 528, 0, motorId + ".Nivel", "Nivel de tanque");
            AddWidget(TipoWidget.Tendencia, 0, 264, motorId + ".RPM", "Historico RPM");
            AddWidget(TipoWidget.PanelAlarmas, 352, 264, motorId + ".Alarma", "Alarmas");
        }

        private void AddWidget(TipoWidget type, double left, double top)
        {
            AddWidget(type, left, top, ResolveDefaultTagFor(type), DefaultTitleFor(type));
        }

        private void AddWidget(TipoWidget type, double left, double top, string tag, string title)
        {
            var width = type == TipoWidget.Tendencia || type == TipoWidget.PanelAlarmas ? CellSize * 4 : CellSize * 3;
            var height = type == TipoWidget.Tendencia || type == TipoWidget.PanelAlarmas ? CellSize * 2 : CellSize * 2;
            AddWidget(type, left, top, tag, title, width, height, null);
        }

        private void AddWidget(TipoWidget type, double left, double top, string tag, string title, double width, double height, string color)
        {
            EmptyDashboardText.Visibility = Visibility.Collapsed;

            var widget = CreateWidgetViewModel(type, tag, title, color);
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = new LinearGradientBrush(
                    Color.FromRgb(18, 30, 46),
                    Color.FromRgb(12, 20, 32),
                    new Point(0, 0),
                    new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 64, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Child = BuildWidgetContent(widget),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 16,
                    Direction = 270,
                    ShadowDepth = 4,
                    Opacity = 0.32,
                    Color = Color.FromRgb(0, 0, 0)
                }
            };

            widget.Container = border;
            border.Tag = widget;
            border.MouseLeftButtonDown += Widget_MouseLeftButtonDown;
            border.MouseRightButtonDown += Widget_MouseRightButtonDown;
            border.ContextMenu = BuildWidgetContextMenu(widget);

            Canvas.SetLeft(border, left);
            Canvas.SetTop(border, top);
            DashboardCanvas.Children.Add(border);
            _widgets.Add(widget);
            AnimateWidgetEntrance(border);
            ApplyCurrentValue(widget);
        }

        private static void AnimateWidgetEntrance(UIElement element)
        {
            element.Opacity = 0;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(0.96, 0.96);

            element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            var transform = element.RenderTransform as ScaleTransform;
            if (transform != null)
            {
                transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
        }

        private ContextMenu BuildWidgetContextMenu(DashboardWidgetViewModel widget)
        {
            var menu = new ContextMenu();
            var deleteItem = new MenuItem { Header = "Eliminar widget" };
            deleteItem.Click += (sender, args) => RemoveWidget(widget);
            menu.Items.Add(deleteItem);
            return menu;
        }

        private DashboardWidgetViewModel CreateWidgetViewModel(TipoWidget type, string tag, string title, string color = null)
        {
            var accent = ParseColor(color) ?? AccentFor(tag, type);
            return new DashboardWidgetViewModel
            {
                Type = type,
                Tag = tag,
                Title = title,
                ValueText = "--",
                Unit = UnitFor(tag),
                Color = ColorToText(accent),
                AccentBrush = new SolidColorBrush(accent),
                BadgeBrush = new SolidColorBrush(Color.FromArgb(32, accent.R, accent.G, accent.B))
            };
        }

        private UIElement BuildWidgetContent(DashboardWidgetViewModel widget)
        {
            switch (widget.Type)
            {
                case TipoWidget.Medidor:
                    return BuildGaugeWidget(widget);
                case TipoWidget.Tanque:
                    return BuildTankWidget(widget);
                case TipoWidget.Tendencia:
                    return BuildTrendWidget(widget);
                case TipoWidget.Led:
                case TipoWidget.Motor:
                    return BuildStateWidget(widget);
                case TipoWidget.PanelAlarmas:
                    return BuildAlarmWidget(widget);
                default:
                    return BuildMetricWidget(widget);
            }
        }

        private UIElement BuildMetricWidget(DashboardWidgetViewModel widget)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(158, 200, 234)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumnSpan(title, 2);
            root.Children.Add(title);

            var valueLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 8, 0)
            };
            var value = new TextBlock
            {
                Text = widget.ValueText,
                FontSize = 30,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 8,
                    Direction = 0,
                    ShadowDepth = 0,
                    Opacity = 0.25,
                    Color = Color.FromRgb(255, 255, 255)
                }
            };
            valueLine.Children.Add(value);
            valueLine.Children.Add(new TextBlock
            {
                Text = " " + widget.Unit,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 188, 209)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            Grid.SetRow(valueLine, 1);
            Grid.SetColumnSpan(valueLine, 2);
            root.Children.Add(valueLine);

            var badge = new Border
            {
                Width = 50,
                Height = 50,
                Background = widget.BadgeBrush,
                CornerRadius = new CornerRadius(8),
                BorderBrush = widget.AccentBrush,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = IconFor(widget.Tag),
                    FontSize = 19,
                    Foreground = widget.AccentBrush,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(badge, 1);
            Grid.SetRow(badge, 1);
            root.Children.Add(badge);

            var footer = new TextBlock
            {
                Text = widget.Tag,
                Foreground = new SolidColorBrush(Color.FromRgb(93, 120, 148)),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 6, 0, 0)
            };
            Grid.SetRow(footer, 2);
            Grid.SetColumnSpan(footer, 2);
            root.Children.Add(footer);

            widget.ValueBlock = value;
            widget.FooterBlock = footer;
            return root;
        }

        private UIElement BuildGaugeWidget(DashboardWidgetViewModel widget)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(158, 200, 234)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });

            var gauge = new Canvas { Width = 172, Height = 104, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            for (var i = 0; i < 9; i++)
            {
                var angle = 205 + i * 16;
                var tick = new Line
                {
                    X1 = 86 + Math.Cos(angle * Math.PI / 180) * 58,
                    Y1 = 82 + Math.Sin(angle * Math.PI / 180) * 58,
                    X2 = 86 + Math.Cos(angle * Math.PI / 180) * 72,
                    Y2 = 82 + Math.Sin(angle * Math.PI / 180) * 72,
                    Stroke = i >= 6 ? widget.AccentBrush : new SolidColorBrush(Color.FromRgb(55, 74, 100)),
                    StrokeThickness = 3
                };
                gauge.Children.Add(tick);
            }

            var needle = new Line
            {
                X1 = 86,
                Y1 = 82,
                X2 = 86,
                Y2 = 25,
                Stroke = widget.AccentBrush,
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                RenderTransformOrigin = new Point(0.5, 1)
            };
            needle.RenderTransform = new RotateTransform(-55, 86, 82);
            gauge.Children.Add(needle);
            gauge.Children.Add(new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(Color.FromRgb(15, 23, 34)),
                Stroke = widget.AccentBrush,
                StrokeThickness = 3
            });
            Canvas.SetLeft(gauge.Children[gauge.Children.Count - 1], 77);
            Canvas.SetTop(gauge.Children[gauge.Children.Count - 1], 73);

            Grid.SetRow(gauge, 1);
            root.Children.Add(gauge);

            var value = new TextBlock
            {
                Text = widget.ValueText,
                FontSize = 27,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(value, 2);
            root.Children.Add(value);

            widget.ValueBlock = value;
            widget.Needle = needle;
            return root;
        }

        private UIElement BuildTankWidget(DashboardWidgetViewModel widget)
        {
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            var title = new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(158, 200, 234)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumnSpan(title, 2);
            root.Children.Add(title);

            var tankShell = new Border
            {
                Width = 96,
                Height = 118,
                CornerRadius = new CornerRadius(18, 18, 10, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(91, 123, 150)),
                BorderThickness = new Thickness(2),
                Background = new LinearGradientBrush(Color.FromRgb(13, 24, 37), Color.FromRgb(23, 40, 56), new Point(0, 0), new Point(1, 1)),
                ClipToBounds = true,
                Margin = new Thickness(0, 12, 16, 0),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 14,
                    Direction = 0,
                    ShadowDepth = 0,
                    Opacity = 0.22,
                    Color = Color.FromRgb(79, 163, 255)
                }
            };

            var tankGrid = new Grid();
            var liquid = new Border
            {
                Height = 0,
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(12, 12, 7, 7),
                Background = BuildLiquidBrush(50)
            };
            tankGrid.Children.Add(liquid);
            tankGrid.Children.Add(new Rectangle
            {
                Fill = new LinearGradientBrush(Color.FromArgb(76, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(1, 0)),
                Width = 26,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(13, 8, 0, 8),
                RadiusX = 8,
                RadiusY = 8,
                IsHitTestVisible = false
            });
            tankShell.Child = tankGrid;

            Grid.SetRow(tankShell, 1);
            root.Children.Add(tankShell);

            var info = new StackPanel { Margin = new Thickness(0, 16, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var value = new TextBlock
            {
                Text = widget.ValueText,
                FontSize = 30,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            info.Children.Add(value);
            info.Children.Add(new TextBlock
            {
                Text = widget.Unit,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 188, 209)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, -2, 0, 8)
            });
            info.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 24, 40)),
                BorderBrush = widget.AccentBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = "Nivel activo",
                    Foreground = widget.AccentBrush,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold
                }
            });
            info.Children.Add(new TextBlock
            {
                Text = widget.Tag,
                Foreground = new SolidColorBrush(Color.FromRgb(93, 120, 148)),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 10, 0, 0)
            });

            Grid.SetColumn(info, 1);
            Grid.SetRow(info, 1);
            root.Children.Add(info);

            widget.ValueBlock = value;
            widget.TankLiquid = liquid;
            widget.TankShell = tankShell;
            return root;
        }

        private UIElement BuildTrendWidget(DashboardWidgetViewModel widget)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            root.Children.Add(new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(158, 200, 234)),
                FontSize = 13,
                FontWeight = FontWeights.Bold
            });

            var canvas = new Canvas { Height = 122, Margin = new Thickness(0, 16, 0, 0), Background = new SolidColorBrush(Color.FromRgb(8, 14, 24)) };
            for (var y = 20; y <= 100; y += 20)
            {
                canvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = 310,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(30, 48, 70)),
                    StrokeDashArray = new DoubleCollection { 3, 3 },
                    StrokeThickness = 1
                });
            }

            var line = new Polyline
            {
                Stroke = widget.AccentBrush,
                StrokeThickness = 3
            };
            canvas.Children.Add(line);

            Grid.SetRow(canvas, 1);
            root.Children.Add(canvas);
            widget.TrendLine = line;
            widget.TrendCanvas = canvas;
            return root;
        }

        private UIElement BuildStateWidget(DashboardWidgetViewModel widget)
        {
            var root = BuildMetricWidget(widget) as Grid;
            var status = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(8, 47, 73)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 74, 0, 0),
                Child = new TextBlock
                {
                    Text = "EN MARCHA",
                    Foreground = new SolidColorBrush(Color.FromRgb(45, 212, 191)),
                    FontWeight = FontWeights.Bold
                }
            };
            Grid.SetColumnSpan(status, 2);
            root.Children.Add(status);
            return root;
        }

        private UIElement BuildAlarmWidget(DashboardWidgetViewModel widget)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());

            var title = new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(158, 200, 234)),
                FontSize = 13,
                FontWeight = FontWeights.Bold
            };
            root.Children.Add(title);

            var value = new TextBlock
            {
                Text = "Sin alarmas",
                Foreground = new SolidColorBrush(Color.FromRgb(45, 212, 191)),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 8)
            };
            Grid.SetRow(value, 1);
            root.Children.Add(value);

            var list = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(7, 14, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 42, 58)),
                BorderThickness = new Thickness(1),
                Foreground = Brushes.White,
                FontSize = 11,
                Padding = new Thickness(4),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetRow(list, 2);
            root.Children.Add(list);

            widget.ValueBlock = value;
            widget.AlarmList = list;
            widget.AlarmItems = GetAlarmHistory(MotorIdFromTag(widget.Tag));
            RenderAlarmList(widget);
            return root;
        }

        private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null)
            {
                return;
            }

            border.BeginAnimation(Canvas.LeftProperty, null);
            border.BeginAnimation(Canvas.TopProperty, null);

            _draggingWidget = border.Tag as DashboardWidgetViewModel;
            SelectWidget(_draggingWidget);
            _dragOffset = e.GetPosition(border);
            _wasDragged = false;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 199, 164));
            border.Opacity = 0.92;
            Panel.SetZIndex(border, 5);
            border.CaptureMouse();
            e.Handled = true;
        }

        private void Widget_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null)
            {
                return;
            }

            SelectWidget(border.Tag as DashboardWidgetViewModel);
            e.Handled = false;
        }

        private void SelectWidget(DashboardWidgetViewModel widget)
        {
            if (_selectedWidget != null && _selectedWidget.Container != null)
            {
                _selectedWidget.Container.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 70, 94));
                _selectedWidget.Container.BorderThickness = new Thickness(1);
            }

            _selectedWidget = widget;

            if (_selectedWidget != null && _selectedWidget.Container != null)
            {
                _selectedWidget.Container.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 199, 164));
                _selectedWidget.Container.BorderThickness = new Thickness(2);
            }
        }

        private void RemoveWidget(DashboardWidgetViewModel widget)
        {
            if (widget == null || widget.Container == null)
            {
                return;
            }

            DashboardCanvas.Children.Remove(widget.Container);
            _widgets.Remove(widget);

            if (_selectedWidget == widget)
            {
                _selectedWidget = null;
            }

            if (_draggingWidget == widget)
            {
                _draggingWidget = null;
            }

            EmptyDashboardText.Visibility = _widgets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AddEvent("Widget eliminado: " + widget.Title);
        }

        private void UpdateTagGrid(string tag, object value)
        {
            TagRowViewModel row;
            if (!_tagRows.TryGetValue(tag, out row))
            {
                row = new TagRowViewModel { Tag = tag };
                _tagRows[tag] = row;
                Tags.Insert(0, row);

                if (Tags.Count > 80)
                {
                    Tags.RemoveAt(Tags.Count - 1);
                }
            }

            row.Value = FormatValue(value);
        }

        private void UpdateMotorCards(string tag, object value)
        {
            var separator = tag.IndexOf('.');
            if (separator <= 0)
            {
                return;
            }

            var motorId = tag.Substring(0, separator);
            var metric = tag.Substring(separator + 1);
            MotorCardViewModel motor;
            if (!_motorMap.TryGetValue(motorId, out motor))
            {
                return;
            }

            if (string.Equals(metric, "RPM", StringComparison.OrdinalIgnoreCase))
            {
                motor.RpmText = FormatValue(value);
            }
            else if (string.Equals(metric, "Temperatura", StringComparison.OrdinalIgnoreCase))
            {
                motor.TemperatureText = FormatValue(value) + " C";
            }
            else if (string.Equals(metric, "Eficiencia", StringComparison.OrdinalIgnoreCase))
            {
                motor.Efficiency = ToDouble(value);
            }
            else if (string.Equals(metric, "Estado", StringComparison.OrdinalIgnoreCase))
            {
                motor.StateText = Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private void UpdateDashboardWidgets(string tag, object value)
        {
            foreach (var widget in _widgets.Where(w => TagsMatch(w.Tag, tag) || w.Type == TipoWidget.PanelAlarmas))
            {
                if (widget.Type == TipoWidget.PanelAlarmas && (!EndsWithTag(tag, ".Alarma") || !IsWidgetForTagMotor(widget, tag)))
                {
                    continue;
                }

                var text = widget.Type == TipoWidget.PanelAlarmas
                    ? (string.IsNullOrWhiteSpace(Convert.ToString(value)) ? "Sin alarmas" : Convert.ToString(value))
                    : FormatValue(value);

                widget.ValueText = text;
                if (widget.ValueBlock != null)
                {
                    widget.ValueBlock.Text = text;
                    widget.ValueBlock.Foreground = widget.Type == TipoWidget.PanelAlarmas && text != "Sin alarmas"
                        ? new SolidColorBrush(Color.FromRgb(255, 106, 106))
                        : new SolidColorBrush(Color.FromRgb(238, 244, 248));
                }

                var number = ToDouble(value);
                if (widget.Progress != null)
                {
                    widget.Progress.Value = Math.Max(0, Math.Min(100, number));
                }

                if (widget.TankLiquid != null)
                {
                    UpdateTankVisual(widget, number);
                }

                if (widget.Needle != null)
                {
                    var max = EndsWithTag(widget.Tag, ".RPM") ? 2000 : 100;
                    var angle = -55 + Math.Max(0, Math.Min(1, number / max)) * 110;
                    widget.Needle.RenderTransform = new RotateTransform(angle, 86, 82);
                }

                if (widget.TrendLine != null)
                {
                    widget.History.Add(number);
                    if (widget.History.Count > 80)
                    {
                        widget.History.RemoveAt(0);
                    }
                    RedrawTrend(widget);
                }

                if (widget.Type == TipoWidget.PanelAlarmas && text != "Sin alarmas")
                {
                    var severity = SeverityForAlarmText(text);
                    RegisterAlarm(tag, text, severity, true);
                }
            }
        }

        private void ResetDashboardWidgets()
        {
            foreach (var widget in _widgets)
            {
                widget.ValueText = "--";
                widget.History.Clear();

                if (widget.ValueBlock != null)
                {
                    widget.ValueBlock.Text = "--";
                    widget.ValueBlock.Foreground = new SolidColorBrush(Color.FromRgb(238, 244, 248));
                }

                if (widget.Progress != null)
                {
                    widget.Progress.Value = 0;
                }

                if (widget.TankLiquid != null)
                {
                    widget.TankLiquid.BeginAnimation(HeightProperty, null);
                    widget.TankLiquid.Height = 0;
                }

                if (widget.Needle != null)
                {
                    widget.Needle.RenderTransform = new RotateTransform(-55, 86, 82);
                }

                if (widget.TrendLine != null)
                {
                    widget.TrendLine.Points = new PointCollection();
                }

                if (widget.AlarmList != null)
                {
                    RenderAlarmList(widget);
                }
            }
        }

        private void ApplyCurrentValue(DashboardWidgetViewModel widget)
        {
            if (widget == null || string.IsNullOrWhiteSpace(widget.Tag))
            {
                return;
            }

            var current = _simulationManager.GetTagValue(widget.Tag);
            if (current != null)
            {
                UpdateDashboardWidgets(widget.Tag, current);
            }
        }

        private void GuardarDashboardActual()
        {
            try
            {
                if (_currentProjectId <= 0)
                {
                    if (_startupOptions.UserId <= 0)
                    {
                        throw new InvalidOperationException("No hay usuario autenticado para asociar el proyecto.");
                    }

                    var proyecto = _dashboardService.CrearProyecto(
                        _startupOptions.UserId,
                        string.IsNullOrWhiteSpace(_startupOptions.ProjectName) ? ProjectNameHeaderTextBlock.Text : _startupOptions.ProjectName);
                    _currentProjectId = proyecto.IdProyecto;
                    _startupOptions.ProjectId = proyecto.IdProyecto;
                    _startupOptions.ProjectName = proyecto.Nombre;
                }

                if (_currentDashboardId <= 0)
                {
                    var dashboard = _dashboardService.CrearDashboard(
                        _currentProjectId,
                        string.IsNullOrWhiteSpace(_startupOptions.DashboardName) ? "Dashboard SCADA" : _startupOptions.DashboardName);
                    _currentDashboardId = dashboard.IdDashboard;
                    _startupOptions.DashboardId = dashboard.IdDashboard;
                    _startupOptions.DashboardName = dashboard.Nombre;
                }

                var layoutJson = SerializeDashboardLayout();
                _dashboardService.GuardarLayout(
                    _currentDashboardId,
                    _startupOptions.DashboardName,
                    _currentProjectId,
                    layoutJson,
                    BuildWidgetPersistenceItems());

                _startupOptions.LayoutJson = layoutJson;
                _startupOptions.LoadFromLayout = true;
                AddEvent("Dashboard guardado en Oracle con " + _widgets.Count + " widgets");
                StatusBarTextBlock.Text = "Dashboard guardado correctamente";
            }
            catch (Exception ex)
            {
                AddEvent("Error al guardar dashboard: " + ex.Message);
                StatusBarTextBlock.Text = "No se pudo guardar el dashboard";
            }
        }

        private List<DashboardWidgetPersistenceItem> BuildWidgetPersistenceItems()
        {
            return _widgets.Select(w =>
            {
                var left = w.Container == null ? 0 : Canvas.GetLeft(w.Container);
                var top = w.Container == null ? 0 : Canvas.GetTop(w.Container);
                return new DashboardWidgetPersistenceItem
                {
                    TipoWidget = w.Type,
                    Tag = w.Tag,
                    Titulo = w.Title,
                    X = double.IsNaN(left) ? 0 : left,
                    Y = double.IsNaN(top) ? 0 : top,
                    Width = w.Container == null ? 0 : w.Container.Width,
                    Height = w.Container == null ? 0 : w.Container.Height,
                    Color = w.Color
                };
            }).ToList();
        }

        private string SerializeDashboardLayout()
        {
            var layout = new DashboardLayoutDocument
            {
                projectId = _currentProjectId,
                dashboardId = _currentDashboardId,
                dashboardName = _startupOptions.DashboardName,
                motorId = _selectedMotorId,
                savedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                widgets = _widgets.Select(w =>
                {
                    var left = w.Container == null ? 0 : Canvas.GetLeft(w.Container);
                    var top = w.Container == null ? 0 : Canvas.GetTop(w.Container);
                    return new DashboardWidgetLayout
                    {
                        tipo = w.Type.ToString(),
                        x = double.IsNaN(left) ? 0 : left,
                        y = double.IsNaN(top) ? 0 : top,
                        width = w.Container == null ? 0 : w.Container.Width,
                        height = w.Container == null ? 0 : w.Container.Height,
                        tag = w.Tag,
                        titulo = w.Title,
                        color = w.Color,
                        sensor = w.Tag
                    };
                }).ToList()
            };

            return new JavaScriptSerializer().Serialize(layout);
        }

        private void LoadDashboardFromJson(string layoutJson)
        {
            DashboardLayoutDocument layout;
            try
            {
                layout = new JavaScriptSerializer().Deserialize<DashboardLayoutDocument>(layoutJson);
            }
            catch
            {
                layout = null;
            }

            if (layout == null || layout.widgets == null || layout.widgets.Count == 0)
            {
                AddEvent("El dashboard no tiene widgets guardados");
                return;
            }

            if (!string.IsNullOrWhiteSpace(layout.motorId))
            {
                SetSelectedMotor(layout.motorId);
            }

            foreach (var savedWidget in layout.widgets)
            {
                AddWidget(
                    WidgetTypeFromLayout(savedWidget.tipo),
                    savedWidget.x,
                    savedWidget.y,
                    string.IsNullOrWhiteSpace(savedWidget.tag) ? savedWidget.sensor : savedWidget.tag,
                    string.IsNullOrWhiteSpace(savedWidget.titulo) ? DefaultTitleFor(WidgetTypeFromLayout(savedWidget.tipo)) : savedWidget.titulo,
                    savedWidget.width > 0 ? savedWidget.width : CellSize * 3,
                    savedWidget.height > 0 ? savedWidget.height : CellSize * 2,
                    savedWidget.color);
            }
        }

        private static TipoWidget WidgetTypeFromLayout(string tipo)
        {
            if (string.Equals(tipo, "grafica", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo, "grafico", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo, "chart", StringComparison.OrdinalIgnoreCase))
            {
                return TipoWidget.Tendencia;
            }

            TipoWidget parsed;
            return Enum.TryParse(tipo, true, out parsed) ? parsed : TipoWidget.Numerico;
        }

        private static void RedrawTrend(DashboardWidgetViewModel widget)
        {
            if (widget.History.Count == 0)
            {
                return;
            }

            var max = Math.Max(1, widget.History.Max());
            var min = widget.History.Min();
            var range = Math.Max(1, max - min);
            var width = Math.Max(220, widget.TrendCanvas.ActualWidth > 0 ? widget.TrendCanvas.ActualWidth : 310);
            var height = 110.0;
            var points = new PointCollection();

            for (var i = 0; i < widget.History.Count; i++)
            {
                var x = widget.History.Count == 1 ? 0 : i * (width / (widget.History.Count - 1));
                var y = height - ((widget.History[i] - min) / range * 88) + 8;
                points.Add(new Point(x, y));
            }

            widget.TrendLine.Points = points;
        }

        private void AddEvent(string text)
        {
            Events.Insert(0, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + text);
            if (Events.Count > 25)
            {
                Events.RemoveAt(Events.Count - 1);
            }
        }

        private static double Snap(double value)
        {
            return Math.Max(0, Math.Round(value / CellSize) * CellSize);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static string FormatValue(object value)
        {
            double number;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
            {
                return number.ToString("0.0", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object value)
        {
            double number;
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out number)
                ? number
                : 0;
        }

        private static string UnitFor(string tag)
        {
            if (EndsWithTag(tag, ".RPM")) return "RPM";
            if (EndsWithTag(tag, ".Temperatura")) return "C";
            if (EndsWithTag(tag, ".Nivel")) return "%";
            if (EndsWithTag(tag, ".Presion")) return "psi";
            if (EndsWithTag(tag, ".Vibracion")) return "mm/s";
            if (EndsWithTag(tag, ".Corriente")) return "A";
            if (EndsWithTag(tag, ".Voltaje")) return "V";
            return string.Empty;
        }

        private static string IconFor(string tag)
        {
            if (EndsWithTag(tag, ".Temperatura")) return "T";
            if (EndsWithTag(tag, ".Corriente")) return "A";
            if (EndsWithTag(tag, ".Voltaje")) return "V";
            if (EndsWithTag(tag, ".Nivel")) return "%";
            if (EndsWithTag(tag, ".Estado")) return "ON";
            return "kW";
        }

        private static Color AccentFor(string tag, TipoWidget type)
        {
            if (type == TipoWidget.PanelAlarmas) return Color.FromRgb(56, 189, 248);
            if (EndsWithTag(tag, ".RPM")) return Color.FromRgb(163, 230, 53);
            if (EndsWithTag(tag, ".Temperatura")) return Color.FromRgb(249, 115, 22);
            if (EndsWithTag(tag, ".Corriente")) return Color.FromRgb(56, 189, 248);
            if (EndsWithTag(tag, ".Vibracion")) return Color.FromRgb(232, 121, 249);
            if (EndsWithTag(tag, ".Voltaje")) return Color.FromRgb(249, 115, 22);
            if (EndsWithTag(tag, ".Nivel")) return Color.FromRgb(34, 211, 238);
            if (EndsWithTag(tag, ".Estado")) return Color.FromRgb(16, 185, 129);
            return Color.FromRgb(251, 191, 36);
        }

        private static Brush BuildLiquidBrush(double level)
        {
            var color = ColorForLevel(level);
            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(235, 255, 255, 255), 0),
                    new GradientStop(color, 0.16),
                    new GradientStop(Color.FromRgb((byte)Math.Max(0, color.R - 22), (byte)Math.Max(0, color.G - 22), (byte)Math.Max(0, color.B - 22)), 1)
                }
            };
        }

        private static Color ColorForLevel(double level)
        {
            if (level < 20) return Color.FromRgb(239, 68, 68);
            if (level < 40) return Color.FromRgb(249, 115, 22);
            if (level > 88) return Color.FromRgb(59, 130, 246);
            return Color.FromRgb(34, 211, 238);
        }

        private void UpdateTankVisual(DashboardWidgetViewModel widget, double level)
        {
            var bounded = Math.Max(0, Math.Min(100, level));
            var maxHeight = widget.TankShell == null ? 108 : Math.Max(90, widget.TankShell.Height - 10);
            var targetHeight = maxHeight * bounded / 100;
            widget.TankLiquid.Background = BuildLiquidBrush(bounded);
            widget.TankLiquid.BeginAnimation(HeightProperty, new DoubleAnimation(widget.TankLiquid.Height, targetHeight, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private bool IsWidgetForTagMotor(DashboardWidgetViewModel widget, string tag)
        {
            return widget != null
                && string.Equals(MotorIdFromTag(widget.Tag), MotorIdFromTag(tag), StringComparison.OrdinalIgnoreCase);
        }

        private static string SeverityForAlarmText(string text)
        {
            var normalized = (text ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("critica") || normalized.Contains("falla") || normalized.Contains("paro")) return "Critica";
            if (normalized.Contains("advertencia") || normalized.Contains("alerta") || normalized.Contains("sobrecarga")) return "Advertencia";
            return "Normal";
        }

        private static bool IsCriticalSeverity(string severity)
        {
            return string.Equals(severity, "Critica", StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase);
        }

        private static Color ColorForSeverity(string severity)
        {
            if (IsCriticalSeverity(severity)) return Color.FromRgb(239, 68, 68);
            if (string.Equals(severity, "Advertencia", StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(245, 158, 11);
            return Color.FromRgb(34, 197, 94);
        }

        private static string IconForSeverity(string severity)
        {
            if (IsCriticalSeverity(severity)) return "[CRIT]";
            if (string.Equals(severity, "Advertencia", StringComparison.OrdinalIgnoreCase)
                || string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase)) return "[WARN]";
            return "[OK]";
        }

        private static Color? ParseColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return null;
            }

            try
            {
                return (Color)ColorConverter.ConvertFromString(color);
            }
            catch
            {
                return null;
            }
        }

        private static string ColorToText(Color color)
        {
            return "#" + color.R.ToString("X2", CultureInfo.InvariantCulture)
                + color.G.ToString("X2", CultureInfo.InvariantCulture)
                + color.B.ToString("X2", CultureInfo.InvariantCulture);
        }

        private string ResolveDefaultTagFor(TipoWidget type)
        {
            var preferredSuffixes = PreferredTagSuffixes(type);
            foreach (var suffix in preferredSuffixes)
            {
                var liveTag = _simulationManager.Tags.Keys.FirstOrDefault(tag =>
                    tag.StartsWith((_selectedMotorId ?? "MOTOR_01") + ".", StringComparison.OrdinalIgnoreCase)
                    && EndsWithTag(tag, suffix));
                if (!string.IsNullOrWhiteSpace(liveTag))
                {
                    return liveTag;
                }
            }

            return DefaultTagFor(type, _selectedMotorId);
        }

        private static string[] PreferredTagSuffixes(TipoWidget type)
        {
            switch (type)
            {
                case TipoWidget.Tanque:
                    return new[] { ".Nivel", ".Level" };
                case TipoWidget.Led:
                    return new[] { ".Estado", ".Alarma" };
                case TipoWidget.Tendencia:
                    return new[] { ".RPM", ".Temperatura", ".Vibracion", ".Corriente" };
                case TipoWidget.PanelAlarmas:
                    return new[] { ".Alarma" };
                case TipoWidget.Numerico:
                    return new[] { ".Temperatura", ".Vibracion", ".Corriente", ".Voltaje", ".RPM" };
                case TipoWidget.Motor:
                case TipoWidget.Medidor:
                default:
                    return new[] { ".RPM", ".Temperatura", ".Vibracion" };
            }
        }

        private static bool TagsMatch(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSelectedMotorTag(string tag)
        {
            return string.IsNullOrWhiteSpace(_selectedMotorId)
                || (!string.IsNullOrWhiteSpace(tag)
                    && tag.StartsWith(_selectedMotorId + ".", StringComparison.OrdinalIgnoreCase));
        }

        private static string MotorIdFromTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return string.Empty;
            }

            var separator = tag.IndexOf('.');
            return separator > 0 ? tag.Substring(0, separator) : string.Empty;
        }

        private static bool EndsWithTag(string tag, string suffix)
        {
            return !string.IsNullOrWhiteSpace(tag)
                && tag.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static string DefaultTagFor(TipoWidget type, string motorId)
        {
            motorId = string.IsNullOrWhiteSpace(motorId) ? "MOTOR_01" : motorId;
            switch (type)
            {
                case TipoWidget.Tanque:
                    return motorId + ".Nivel";
                case TipoWidget.Led:
                    return motorId + ".Estado";
                case TipoWidget.Tendencia:
                    return motorId + ".Temperatura";
                case TipoWidget.PanelAlarmas:
                    return motorId + ".Alarma";
                case TipoWidget.Motor:
                    return motorId + ".RPM";
                default:
                    return motorId + ".RPM";
            }
        }

        private static string DefaultTitleFor(TipoWidget type)
        {
            switch (type)
            {
                case TipoWidget.Tanque:
                    return "Nivel de tanque";
                case TipoWidget.Led:
                    return "Estado";
                case TipoWidget.Tendencia:
                    return "Tendencia";
                case TipoWidget.PanelAlarmas:
                    return "Alarmas";
                case TipoWidget.Motor:
                    return "Motor";
                case TipoWidget.Numerico:
                    return "Valor numerico";
                default:
                    return "Gauge";
            }
        }
    }

    public class WidgetPaletteItem
    {
        public WidgetPaletteItem(TipoWidget type, string name, string description)
        {
            Type = type;
            Name = name;
            Description = description;
        }

        public TipoWidget Type { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
    }

    public class DashboardWidgetViewModel
    {
        public DashboardWidgetViewModel()
        {
            History = new List<double>();
        }

        public TipoWidget Type { get; set; }
        public string Tag { get; set; }
        public string Title { get; set; }
        public string ValueText { get; set; }
        public string Unit { get; set; }
        public string Color { get; set; }
        public Brush AccentBrush { get; set; }
        public Brush BadgeBrush { get; set; }
        public Border Container { get; set; }
        public TextBlock ValueBlock { get; set; }
        public TextBlock FooterBlock { get; set; }
        public ProgressBar Progress { get; set; }
        public Line Needle { get; set; }
        public Polyline TrendLine { get; set; }
        public Canvas TrendCanvas { get; set; }
        public Border TankLiquid { get; set; }
        public Border TankShell { get; set; }
        public ListBox AlarmList { get; set; }
        public ObservableCollection<AlarmEventViewModel> AlarmItems { get; set; }
        public List<double> History { get; private set; }
    }

    public class AlarmEventViewModel
    {
        public DateTime Time { get; set; }
        public string MotorId { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public string Tag { get; set; }
        public string Icon { get; set; }
        public Brush ColorBrush { get; set; }
    }

    public class TagRowViewModel : NotifyObject
    {
        private string _value;

        public string Tag { get; set; }

        public string Value
        {
            get { return _value; }
            set
            {
                _value = value;
                RaisePropertyChanged("Value");
            }
        }
    }

    public class MotorCardViewModel : NotifyObject
    {
        private string _rpmText = "0.0";
        private string _temperatureText = "25.0 C";
        private string _stateText = "Apagado";
        private double _efficiency;
        private bool _isSelected;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Line { get; set; }

        public string RpmText
        {
            get { return _rpmText; }
            set
            {
                _rpmText = value;
                RaisePropertyChanged("RpmText");
            }
        }

        public string TemperatureText
        {
            get { return _temperatureText; }
            set
            {
                _temperatureText = value;
                RaisePropertyChanged("TemperatureText");
            }
        }

        public string StateText
        {
            get { return _stateText; }
            set
            {
                _stateText = value;
                RaisePropertyChanged("StateText");
                RaisePropertyChanged("StateBrush");
            }
        }

        public double Efficiency
        {
            get { return _efficiency; }
            set
            {
                _efficiency = value;
                RaisePropertyChanged("Efficiency");
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                RaisePropertyChanged("IsSelected");
                RaisePropertyChanged("SelectionBrush");
                RaisePropertyChanged("SelectionThickness");
            }
        }

        public Brush SelectionBrush
        {
            get
            {
                return IsSelected
                    ? new SolidColorBrush(Color.FromRgb(40, 199, 164))
                    : new SolidColorBrush(Color.FromRgb(52, 70, 94));
            }
        }

        public Thickness SelectionThickness
        {
            get { return IsSelected ? new Thickness(2) : new Thickness(1); }
        }

        public Brush StateBrush
        {
            get
            {
                if (StateText == "Falla")
                {
                    return new SolidColorBrush(Color.FromRgb(255, 106, 106));
                }

                if (StateText == "Advertencia")
                {
                    return new SolidColorBrush(Color.FromRgb(255, 204, 102));
                }

                if (StateText == "EnMarcha")
                {
                    return new SolidColorBrush(Color.FromRgb(40, 199, 164));
                }

                return new SolidColorBrush(Color.FromRgb(143, 160, 179));
            }
        }
    }

    public class DashboardLayoutDocument
    {
        public int projectId { get; set; }
        public int dashboardId { get; set; }
        public string dashboardName { get; set; }
        public string motorId { get; set; }
        public string savedAt { get; set; }
        public List<DashboardWidgetLayout> widgets { get; set; }
    }

    public class DashboardWidgetLayout
    {
        public string tipo { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double width { get; set; }
        public double height { get; set; }
        public string tag { get; set; }
        public string sensor { get; set; }
        public string titulo { get; set; }
        public string color { get; set; }
    }

    public abstract class NotifyObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

