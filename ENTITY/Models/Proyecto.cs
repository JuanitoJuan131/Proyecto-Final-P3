using System;
using System.Collections.Generic;

namespace ENTITY.Models
{
    public class Proyecto
    {
        public int IdProyecto { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int IdUsuario { get; set; }
        public Usuario Usuario { get; set; }
        public List<Dashboard> Dashboards { get; } = new List<Dashboard>();
    }
}
