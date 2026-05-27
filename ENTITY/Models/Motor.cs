using System;

namespace ENTITY.Models
{
    public enum EstadoMotor
    {
        Apagado,
        Arrancando,
        EnMarcha,
        Advertencia,
        Falla,
        Deteniendo
    }

    public class TelemetriaMotor
    {
        public TelemetriaMotor(string id, string nombre, string lineaProduccion)
        {
            Id = id;
            Nombre = nombre;
            LineaProduccion = lineaProduccion;
            Temperatura = 25;
            Voltaje = 220;
            Nivel = 50;
            Eficiencia = 100;
            Estado = EstadoMotor.Apagado;
            UltimaActualizacion = DateTime.Now;
        }

        public string Id { get; set; }
        public string Nombre { get; set; }
        public string LineaProduccion { get; set; }
        public double Rpm { get; set; }
        public double Temperatura { get; set; }
        public double Presion { get; set; }
        public double Vibracion { get; set; }
        public double Voltaje { get; set; }
        public double Corriente { get; set; }
        public double Torque { get; set; }
        public double Nivel { get; set; }
        public double ConsumoEnergia { get; set; }
        public double Eficiencia { get; set; }
        public EstadoMotor Estado { get; set; }
        public bool AlarmaActiva { get; set; }
        public string MensajeAlarma { get; set; }
        public DateTime UltimaActualizacion { get; set; }
    }

    public class Motor
    {
        public int IdMotor { get; set; }
        public string Nombre { get; set; }
        public string Modelo { get; set; }
        public string Fabricante { get; set; }
        public string Estado { get; set; }
        public DateTime? FechaInstalacion { get; set; }
        public int IdDispositivo { get; set; }
        public Dispositivo Dispositivo { get; set; }
    }

    public class HistoricoMedicion
    {
        public int IdMedicion { get; set; }
        public double Valor { get; set; }
        public DateTime? FechaMedicion { get; set; }
        public int IdMotor { get; set; }
        public int IdTag { get; set; }
        public Motor Motor { get; set; }
        public TagDato Tag { get; set; }
    }
}
