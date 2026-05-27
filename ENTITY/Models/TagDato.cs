namespace ENTITY.Models
{
    public class TagDato
    {
        public int IdTag { get; set; }
        public string Nombre { get; set; }
        public string Unidad { get; set; }
        public double ValorActual { get; set; }
        public int IdDispositivo { get; set; }
        public Dispositivo Dispositivo { get; set; }
    }
}
