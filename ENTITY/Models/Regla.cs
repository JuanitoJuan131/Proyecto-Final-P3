using System.Collections.Generic;

namespace ENTITY.Models
{
    public class Regla
    {
        public int IdRegla { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public bool Activa { get; set; }
        public int IdDashboard { get; set; }
        public Dashboard Dashboard { get; set; }
        public List<Condicion> Condiciones { get; } = new List<Condicion>();
        public List<Accion> Acciones { get; } = new List<Accion>();
        public List<HistorialEvento> HistorialEventos { get; } = new List<HistorialEvento>();
    }

    public class Condicion
    {
        public int IdCondicion { get; set; }
        public string Operador { get; set; }
        public double ValorUmbral { get; set; }
        public int IdRegla { get; set; }
        public int IdTag { get; set; }
        public Regla Regla { get; set; }
        public TagDato Tag { get; set; }
    }

    public class Accion
    {
        public int IdAccion { get; set; }
        public string TipoAccion { get; set; }
        public string ParametrosJson { get; set; }
        public int IdRegla { get; set; }
        public Regla Regla { get; set; }
    }

    public class HistorialEvento
    {
        public int IdEvento { get; set; }
        public string Descripcion { get; set; }
        public double? ValorDetectado { get; set; }
        public System.DateTime? FechaEvento { get; set; }
        public int? IdRegla { get; set; }
        public int? IdMotor { get; set; }
        public Regla Regla { get; set; }
        public Motor Motor { get; set; }
    }
}
