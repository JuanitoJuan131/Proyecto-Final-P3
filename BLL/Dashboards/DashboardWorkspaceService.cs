using DAL.Repositories;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BLL.Dashboards
{
    public class DashboardWorkspaceService
    {
        private readonly ProyectoRepositorio _proyectoRepositorio = new ProyectoRepositorio();
        private readonly DashboardRepositorio _dashboardRepositorio = new DashboardRepositorio();

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
        }

        public Proyecto ObtenerProyecto(int idProyecto)
        {
            return _proyectoRepositorio.ObtenerPorId(idProyecto);
        }
    }

    public class ProyectoResumen
    {
        public int IdProyecto { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int CantidadDashboards { get; set; }
    }
}
