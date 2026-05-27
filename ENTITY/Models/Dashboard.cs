using Newtonsoft.Json;
using System.Collections.Generic;

namespace ENTITY.Models
{
    public class Dashboard
    {
        public int IdDashboard { get; set; }
        public string Nombre { get; set; }
        public string LayoutJson { get; set; }
        public System.DateTime? FechaCreacion { get; set; }
        public int IdProyecto { get; set; }
        public Proyecto Proyecto { get; set; }
        public List<Widget> Widgets { get; } = new List<Widget>();
        public List<Dispositivo> Dispositivos { get; } = new List<Dispositivo>();
        public List<Regla> Reglas { get; } = new List<Regla>();
        public List<HistorialEvento> HistorialEventos { get; } = new List<HistorialEvento>();

        public ConfiguracionLayout Layout
        {
            get { return JsonConvert.DeserializeObject<ConfiguracionLayout>(LayoutJson ?? "{}") ?? new ConfiguracionLayout(); }
        }
    }

    public class ConfiguracionLayout
    {
        public int Columnas { get; set; } = 12;
        public int Filas { get; set; } = 8;
        public int TamanoCelda { get; set; } = 88;
    }
}
