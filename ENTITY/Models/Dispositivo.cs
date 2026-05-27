using System.Collections.Generic;

namespace ENTITY.Models
{
    public class Dispositivo
    {
        public int IdDispositivo { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        public string Protocolo { get; set; }
        public bool Simulado { get; set; }
        public string IdAzureIot { get; set; }
        public int IdDashboard { get; set; }
        public Dashboard Dashboard { get; set; }
        public List<TagDato> Tags { get; } = new List<TagDato>();
        public List<Motor> Motores { get; } = new List<Motor>();
    }
}
