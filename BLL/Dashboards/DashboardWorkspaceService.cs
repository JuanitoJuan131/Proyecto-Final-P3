using DAL.Repositories;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace BLL.Dashboards
{
    public class DashboardWorkspaceService
    {
        private readonly ProyectoRepositorio _proyectoRepositorio = new ProyectoRepositorio();
        private readonly DashboardRepositorio _dashboardRepositorio = new DashboardRepositorio();
        private readonly WidgetRepositorio _widgetRepositorio = new WidgetRepositorio();
        private readonly TagDatoRepositorio _tagRepositorio = new TagDatoRepositorio();

        public List<ProyectoResumen> ObtenerProyectosRecientes(int idUsuario)
        {
            return _proyectoRepositorio.ObtenerPorUsuario(idUsuario)
                .OrderByDescending(p => p.FechaCreacion)
                .Select(p =>
                {
                    var dashboards = _dashboardRepositorio.ObtenerPorProyecto(p.IdProyecto);
                    return new ProyectoResumen
                    {
                        IdProyecto = p.IdProyecto,
                        Nombre = p.Nombre,
                        FechaCreacion = p.FechaCreacion,
                        CantidadDashboards = dashboards.Count
                    };
                })
                .ToList();
        }

        public Proyecto CrearProyecto(int idUsuario, string nombre)
        {
            var proyecto = new Proyecto
            {
                Nombre = string.IsNullOrWhiteSpace(nombre) ? "Nuevo proyecto industrial" : nombre.Trim(),
                FechaCreacion = DateTime.Now,
                IdUsuario = idUsuario
            };

            _proyectoRepositorio.Insertar(proyecto);
            return proyecto;
        }

        public List<Dashboard> ObtenerDashboardsProyecto(int idProyecto)
        {
            return _dashboardRepositorio.ObtenerPorProyecto(idProyecto)
                .OrderByDescending(d => d.FechaCreacion ?? DateTime.MinValue)
                .ToList();
        }

        public Dashboard ObtenerDashboard(int idDashboard)
        {
            return _dashboardRepositorio.ObtenerPorId(idDashboard);
        }

        public Dashboard CrearDashboard(int idProyecto, string nombre)
        {
            var dashboard = new Dashboard
            {
                Nombre = string.IsNullOrWhiteSpace(nombre) ? "Dashboard SCADA" : nombre.Trim(),
                LayoutJson = "{\"widgets\":[]}",
                FechaCreacion = DateTime.Now,
                IdProyecto = idProyecto
            };

            _dashboardRepositorio.Insertar(dashboard);
            return dashboard;
        }

        public void GuardarLayout(int idDashboard, string nombre, int idProyecto, string layoutJson)
        {
            GuardarLayout(idDashboard, nombre, idProyecto, layoutJson, null);
        }

        public void GuardarLayout(int idDashboard, string nombre, int idProyecto, string layoutJson, IEnumerable<DashboardWidgetPersistenceItem> widgets)
        {
            var dashboard = _dashboardRepositorio.ObtenerPorId(idDashboard);
            if (dashboard == null)
            {
                dashboard = new Dashboard
                {
                    IdDashboard = idDashboard,
                    Nombre = nombre,
                    IdProyecto = idProyecto,
                    FechaCreacion = DateTime.Now
                };
            }

            dashboard.Nombre = string.IsNullOrWhiteSpace(nombre) ? dashboard.Nombre : nombre.Trim();
            dashboard.IdProyecto = idProyecto;
            dashboard.LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? "{\"widgets\":[]}" : layoutJson;
            if (!dashboard.FechaCreacion.HasValue)
            {
                dashboard.FechaCreacion = DateTime.Now;
            }

            _dashboardRepositorio.Actualizar(dashboard);

            if (widgets != null)
            {
                var tagIds = _tagRepositorio.ObtenerTodos()
                    .GroupBy(t => t.Nombre ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().IdTag, StringComparer.OrdinalIgnoreCase);

                var entities = widgets.Select(w => new Widget
                {
                    TipoWidget = w.TipoWidget,
                    Descripcion = string.IsNullOrWhiteSpace(w.Titulo) ? w.TipoWidget.ToString() : w.Titulo.Trim(),
                    PosicionX = w.X,
                    PosicionY = w.Y,
                    IdDashboard = idDashboard,
                    IdTag = ResolveTagId(tagIds, w.Tag),
                    ParametrosJson = new JavaScriptSerializer().Serialize(new
                    {
                        tag = w.Tag,
                        titulo = w.Titulo,
                        width = w.Width,
                        height = w.Height,
                        color = w.Color,
                        sensor = w.Tag
                    })
                }).ToList();

                _widgetRepositorio.ReemplazarPorDashboard(idDashboard, entities);
            }
        }

        public Proyecto ObtenerProyecto(int idProyecto)
        {
            return _proyectoRepositorio.ObtenerPorId(idProyecto);
        }

        private static int? ResolveTagId(Dictionary<string, int> tagIds, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            int id;
            if (tagIds.TryGetValue(tag, out id))
            {
                return id;
            }

            var shortName = tag.Contains(".") ? tag.Substring(tag.LastIndexOf('.') + 1) : tag;
            return tagIds.TryGetValue(shortName, out id) ? (int?)id : null;
        }
    }

    public class ProyectoResumen
    {
        public int IdProyecto { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int CantidadDashboards { get; set; }
    }

    public class DashboardWidgetPersistenceItem
    {
        public TipoWidget TipoWidget { get; set; }
        public string Tag { get; set; }
        public string Titulo { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Color { get; set; }
    }
}
