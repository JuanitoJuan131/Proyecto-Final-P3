using BLL.Security;
using System;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows;

namespace GUI
{
    public partial class LoginWindow : Window 
    {
        private readonly AutenticacionService _autenticacionService = new AutenticacionService();

        public LoginWindow()
        {
            InitializeComponent();
            ResponsiveWindowHelper.Ajustar(this, 1040, 680);
        }

        public DashboardStartupOptions SelectedOptions { get; private set; }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                ShowToast("Campos incompletos", "Ingresa el correo del operador.", ToastSeverity.Warning);
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowToast("Campos incompletos", "Ingresa la contrasena del operador.", ToastSeverity.Warning);
                PasswordBox.Focus();
                return;
            }

            LoginButton.IsEnabled = false;
            LoginStatusTextBlock.Text = "Validando usuario...";
            ShowToast("Validando acceso", "Comprobando credenciales contra Oracle...", ToastSeverity.Info);

            try
            {
                var resultado = _autenticacionService.Autenticar(EmailTextBox.Text, PasswordBox.Password);
                if (!resultado.Autenticado)
                {
                    LoginStatusTextBlock.Text = resultado.Mensaje;
                    ShowToast("Acceso denegado", resultado.Mensaje, ToastSeverity.Error);
                    LoginButton.IsEnabled = true;
                    return;
                }

                ShowToast("Acceso autorizado", "Cargando Centro de Proyectos...", ToastSeverity.Success);
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260))
                {
                    BeginTime = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                fade.Completed += (s, args) =>
                {
                    var projects = new ProyectosRecientes(resultado.Usuario);
                    projects.Show();
                    Close();
                };
                RootGrid.BeginAnimation(OpacityProperty, fade);
            }
            catch (Exception ex)
            {
                LoginStatusTextBlock.Text = ex.Message;
                ShowToast("Conexion fallida", "No fue posible validar con Oracle. " + ex.Message, ToastSeverity.Error);
                LoginButton.IsEnabled = true;
            }
        }

        private void ShowToast(string title, string message, ToastSeverity severity)
        {
            LoginToast.Visibility = Visibility.Visible;
            ToastTitleTextBlock.Text = title;
            ToastMessageTextBlock.Text = message;

            switch (severity)
            {
                case ToastSeverity.Success:
                    LoginToast.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 212, 191));
                    ToastIconTextBlock.Text = "\uE930";
                    ToastIconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(45, 212, 191));
                    break;
                case ToastSeverity.Warning:
                    LoginToast.BorderBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                    ToastIconTextBlock.Text = "\uE7BA";
                    ToastIconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                    break;
                case ToastSeverity.Error:
                    LoginToast.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    ToastIconTextBlock.Text = "\uE783";
                    ToastIconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    break;
                default:
                    LoginToast.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    ToastIconTextBlock.Text = "\uE946";
                    ToastIconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    break;
            }

            LoginToast.BeginAnimation(OpacityProperty, null);
            LoginToast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            if (severity == ToastSeverity.Info)
            {
                return;
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.2) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                fade.Completed += (sender, args) => LoginToast.Visibility = Visibility.Collapsed;
                LoginToast.BeginAnimation(OpacityProperty, fade);
            };
            timer.Start();
        }
    }

    internal enum ToastSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }
}
