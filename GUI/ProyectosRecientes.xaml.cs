using BLL.Dashboards;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace GUI
{
    public partial class ProyectosRecientes : Window
    {
        private readonly Usuario _usuario;
        private readonly DashboardWorkspaceService _workspaceService = new DashboardWorkspaceService();
        private ProyectoListItem _selectedProject;

        public ProyectosRecientes(Usuario usuario)
        {
            _usuario = usuario;
            InitializeComponent();
            ResponsiveWindowHelper.Ajustar(this, 1040, 680);
            Projects = new ObservableCollection<ProyectoListItem>();
            Dashboards = new ObservableCollection<DashboardListItem>();
            DataContext = this;
            UsuarioTextBlock.Text = usuario.Nombre + " - " + usuario.Rol;
            LoadProjects();
        }

        public ObservableCollection<ProyectoListItem> Projects { get; private set; }
        public ObservableCollection<DashboardListItem> Dashboards { get; private set; }

        private void LoadProjects()
        {
            Projects.Clear();
            foreach (var project in _workspaceService.ObtenerProyectosRecientes(_usuario.IdUsuario))
            {
                Projects.Add(new ProyectoListItem(project));
            }

            if (Projects.Count > 0)
            {
                ProjectsListBox.SelectedIndex = 0;
            }
            else
            {
                StatusTextBlock.Text = "No hay proyectos creados para este usuario.";
            }
        }

        private void ProjectsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedProject = ProjectsListBox.SelectedItem as ProyectoListItem;
            LoadDashboardsForSelectedProject();
        }

        private void LoadDashboardsForSelectedProject()
        {
            Dashboards.Clear();
            if (_selectedProject == null)
            {
                return;
            }

            SelectedProjectTextBlock.Text = "Dashboards de " + _selectedProject.Nombre;
            foreach (var dashboard in _workspaceService.ObtenerDashboardsProyecto(_selectedProject.IdProyecto))
            {
                Dashboards.Add(new DashboardListItem(dashboard));
            }

            StatusTextBlock.Text = Dashboards.Count + " dashboards disponibles.";
        }

        private void CreateProjectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var project = _workspaceService.CrearProyecto(_usuario.IdUsuario, ProjectNameTextBox.Text);
                var item = new ProyectoListItem(new ProyectoResumen
                {
                    IdProyecto = project.IdProyecto,
                    Nombre = project.Nombre,
                    FechaCreacion = project.FechaCreacion,
                    CantidadDashboards = 0
                });
                Projects.Insert(0, item);
                ProjectsListBox.SelectedItem = item;
                StatusTextBlock.Text = "Proyecto creado.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "No se pudo crear el proyecto: " + ex.Message;
            }
        }

        private void CreateDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProject == null)
            {
                StatusTextBlock.Text = "Selecciona un proyecto primero.";
                return;
            }

            try
            {
                var dashboard = _workspaceService.CrearDashboard(_selectedProject.IdProyecto, "Dashboard SCADA");
                var item = new DashboardListItem(dashboard);
                Dashboards.Insert(0, item);
                _selectedProject.CantidadDashboards++;
                OpenDashboard(item);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "No se pudo crear el dashboard: " + ex.Message;
            }
        }

        private void OpenDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            var dashboard = (sender as Button)?.Tag as DashboardListItem;
            if (dashboard != null)
            {
                OpenDashboard(dashboard);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var login = new LoginWindow();
            login.Show();
            Close();
        }

        private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var project = (sender as Button)?.Tag as ProyectoListItem;
            if (project == null)
            {
                return;
            }

            ProjectsListBox.SelectedItem = project;
            if (Dashboards.Count > 0)
            {
                OpenDashboard(Dashboards[0]);
            }
            else
            {
                CreateDashboardButton_Click(sender, e);
            }
        }

        private void OpenDashboard(DashboardListItem dashboard)
        {
            var window = new MainWindow(new DashboardStartupOptions
            {
                UserId = _usuario.IdUsuario,
                UserName = _usuario.Nombre,
                UserRole = _usuario.Rol,
                ProjectId = _selectedProject.IdProyecto,
                ProjectName = _selectedProject.Nombre,
                DashboardId = dashboard.IdDashboard,
                DashboardName = dashboard.Nombre,
                LayoutJson = dashboard.LayoutJson,
                LoadFromLayout = !string.IsNullOrWhiteSpace(dashboard.LayoutJson),
                MotorId = dashboard.MotorId,
                TemplateIndex = 0
            });
            window.Show();
            Close();
        }
    }

    public class ProyectoListItem : NotifyObject
    {
        private int _cantidadDashboards;

        public ProyectoListItem(ProyectoResumen resumen)
        {
            IdProyecto = resumen.IdProyecto;
            Nombre = resumen.Nombre;
            FechaCreacion = resumen.FechaCreacion;
            CantidadDashboards = resumen.CantidadDashboards;
        }

        public int IdProyecto { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaCreacion { get; set; }

        public int CantidadDashboards
        {
            get { return _cantidadDashboards; }
            set
            {
                _cantidadDashboards = value;
                RaisePropertyChanged("CantidadDashboards");
                RaisePropertyChanged("DashboardCountText");
            }
        }

        public string FechaTexto
        {
            get { return FechaCreacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture); }
        }

        public string DashboardCountText
        {
            get { return CantidadDashboards + " dashboards"; }
        }
    }

    public class DashboardListItem
    {
        public DashboardListItem(Dashboard dashboard)
        {
            IdDashboard = dashboard.IdDashboard;
            Nombre = dashboard.Nombre;
            FechaCreacion = dashboard.FechaCreacion;
            LayoutJson = dashboard.LayoutJson;
            IdProyecto = dashboard.IdProyecto;
            WidgetCount = CountWidgets(dashboard.LayoutJson);
            MotorId = ResolveMotorId(dashboard.LayoutJson);
        }

        public int IdDashboard { get; set; }
        public int IdProyecto { get; set; }
        public string Nombre { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public string LayoutJson { get; set; }
        public int WidgetCount { get; set; }
        public string MotorId { get; set; }

        public string FechaTexto
        {
            get { return FechaCreacion.HasValue ? FechaCreacion.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "Sin fecha"; }
        }

        public string WidgetCountText
        {
            get { return WidgetCount + " widgets guardados"; }
        }

        private static int CountWidgets(string layoutJson)
        {
            var layout = DeserializeLayout(layoutJson);
            return layout != null && layout.widgets != null ? layout.widgets.Count : 0;
        }

        private static string ResolveMotorId(string layoutJson)
        {
            var layout = DeserializeLayout(layoutJson);
            return layout != null && !string.IsNullOrWhiteSpace(layout.motorId) ? layout.motorId : "MOTOR_01";
        }

        private static DashboardLayoutDocument DeserializeLayout(string layoutJson)
        {
            if (string.IsNullOrWhiteSpace(layoutJson))
            {
                return null;
            }

            try
            {
                return new JavaScriptSerializer().Deserialize<DashboardLayoutDocument>(layoutJson);
            }
            catch
            {
                return null;
            }
        }
    }
}
