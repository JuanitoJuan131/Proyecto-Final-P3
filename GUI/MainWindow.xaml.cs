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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GUI
{
    public partial class MainWindow : Window
    {
        private const int CellSize = 88;
        private readonly DashboardWorkspaceService _dashboardService = new DashboardWorkspaceService();
        private readonly SimulationManager _simulationManager = new SimulationManager();
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
        private readonly Dictionary<string, MotorCardViewModel> _motorMap = new Dictionary<string, MotorCardViewModel>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TagRowViewModel> _tagRows = new Dictionary<string, TagRowViewModel>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DashboardWidgetViewModel> _widgets = new List<DashboardWidgetViewModel>();
        private DashboardWidgetViewModel _draggingWidget;
        private DashboardWidgetViewModel _selectedWidget;
        private Point _dragOffset;
        private bool _wasDragged;
        private bool _simulationRunning = true;
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

                await _simulationManager.ConnectMqttAsync(BrokerTextBox.Text.Trim(), port, TopicTextBox.Text.Trim());
                if (wasSimulationRunning)
                {
                    AddEvent("Simulacion local pausada: usando telemetria MQTT del ESP32");
                }

                StatusBarTextBlock.Text = "Suscrito a " + TopicTextBox.Text.Trim();
                AddEvent("MQTT conectado a " + BrokerTextBox.Text.Trim());
                AddEvent("Esperando datos del ESP32 en " + TopicTextBox.Text.Trim());
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
                SetSelectedMotor(motorId);
                AddEvent("Motor seleccionado para edicion: " + motorId);
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
            });
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
                UpdateTagGrid(tag, value);
                UpdateMotorCards(tag, value);
                UpdateDashboardWidgets(tag, value);
            });
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

        private void AddDefaultWidgets(string motorId)
        {
            motorId = string.IsNullOrWhiteSpace(motorId) ? "MOTOR_01" : motorId;
            AddWidget(TipoWidget.Medidor, 0, 0, motorId + ".RPM", "Velocidad RPM");
            AddWidget(TipoWidget.Numerico, 264, 0, motorId + ".Temperatura", "Temperatura");
            AddWidget(TipoWidget.Numerico, 528, 0, motorId + ".Vibracion", "Vibracion");
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
                Background = new SolidColorBrush(Color.FromRgb(27, 34, 45)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 70, 94)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Child = BuildWidgetContent(widget)
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
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });

            var valueLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var value = new TextBlock
            {
                Text = widget.ValueText,
                FontSize = 26,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            valueLine.Children.Add(value);
            valueLine.Children.Add(new TextBlock
            {
                Text = " " + widget.Unit,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 0, 3)
            });
            stack.Children.Add(valueLine);
            root.Children.Add(stack);

            var badge = new Border
            {
                Width = 42,
                Height = 42,
                Background = widget.BadgeBrush,
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Text = IconFor(widget.Tag),
                    FontSize = 20,
                    Foreground = widget.AccentBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(badge, 1);
            root.Children.Add(badge);

            widget.ValueBlock = value;
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
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });

            var gauge = new Canvas { Width = 160, Height = 100, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            for (var i = 0; i < 9; i++)
            {
                var angle = 205 + i * 16;
                var tick = new Line
                {
                    X1 = 80 + Math.Cos(angle * Math.PI / 180) * 58,
                    Y1 = 80 + Math.Sin(angle * Math.PI / 180) * 58,
                    X2 = 80 + Math.Cos(angle * Math.PI / 180) * 70,
                    Y2 = 80 + Math.Sin(angle * Math.PI / 180) * 70,
                    Stroke = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    StrokeThickness = 3
                };
                gauge.Children.Add(tick);
            }

            var needle = new Line
            {
                X1 = 80,
                Y1 = 80,
                X2 = 80,
                Y2 = 23,
                Stroke = widget.AccentBrush,
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                RenderTransformOrigin = new Point(0.5, 1)
            };
            needle.RenderTransform = new RotateTransform(-55, 80, 80);
            gauge.Children.Add(needle);
            gauge.Children.Add(new Ellipse
            {
                Width = 16,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromRgb(15, 23, 34)),
                Stroke = widget.AccentBrush,
                StrokeThickness = 3
            });
            Canvas.SetLeft(gauge.Children[gauge.Children.Count - 1], 72);
            Canvas.SetTop(gauge.Children[gauge.Children.Count - 1], 72);

            Grid.SetRow(gauge, 1);
            root.Children.Add(gauge);

            var value = new TextBlock
            {
                Text = widget.ValueText,
                FontSize = 24,
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
            var root = BuildMetricWidget(widget) as Grid;
            var bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 8,
                Margin = new Thickness(0, 76, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = widget.AccentBrush,
                Background = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            };
            Grid.SetColumnSpan(bar, 2);
            root.Children.Add(bar);
            widget.Progress = bar;
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
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Bold
            });

            var canvas = new Canvas { Height = 122, Margin = new Thickness(0, 16, 0, 0), Background = new SolidColorBrush(Color.FromRgb(15, 23, 34)) };
            for (var y = 20; y <= 100; y += 20)
            {
                canvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = 310,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                    StrokeDashArray = new DoubleCollection { 3, 3 },
                    StrokeThickness = 1
                });
            }

            var line = new Polyline
            {
                Stroke = widget.AccentBrush,
                StrokeThickness = 2
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
            var root = new StackPanel();
            root.Children.Add(new TextBlock
            {
                Text = widget.Title.ToUpperInvariant(),
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Bold
            });
            var value = new TextBlock
            {
                Text = "Sin alarmas",
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 16, 0, 0)
            };
            root.Children.Add(value);
            root.Children.Add(new TextBlock
            {
                Text = "Registro en tiempo real",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 8, 0, 0)
            });
            widget.ValueBlock = value;
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
                if (widget.Type == TipoWidget.PanelAlarmas && !EndsWithTag(tag, ".Alarma"))
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

                if (widget.Needle != null)
                {
                    var max = EndsWithTag(widget.Tag, ".RPM") ? 2000 : 100;
                    var angle = -55 + Math.Max(0, Math.Min(1, number / max)) * 110;
                    widget.Needle.RenderTransform = new RotateTransform(angle, 80, 80);
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

                if (widget.Needle != null)
                {
                    widget.Needle.RenderTransform = new RotateTransform(-55, 80, 80);
                }

                if (widget.TrendLine != null)
                {
                    widget.TrendLine.Points = new PointCollection();
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
                    layoutJson);

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
        public List<double> History { get; private set; }
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

