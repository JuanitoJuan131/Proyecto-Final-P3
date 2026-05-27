using ENTITY.Models;
using System;

namespace BLL.Simulation
{
    public class SensorSimulator
    {
        public void Update(TelemetriaMotor motor)
        {
            var carga = IndustrialRandom.Range(0.55, 1.08);
            motor.Temperatura = Clamp(motor.Temperatura + IndustrialRandom.Range(-0.35, 0.75) + (carga > 0.95 ? 0.35 : 0), 25, 112);
            motor.Presion = IndustrialRandom.Range(82, 110) + (carga > 1.0 ? IndustrialRandom.Range(2, 8) : 0);
            motor.Vibracion = IndustrialRandom.Range(0.8, 4.6) + (motor.Rpm > 1780 ? IndustrialRandom.Range(0.6, 1.5) : 0);
            motor.Voltaje = IndustrialRandom.Range(211, 229);
            motor.Corriente = IndustrialRandom.Range(15, 31) * carga;
            motor.Torque = IndustrialRandom.Range(55, 92) * carga;
            motor.Nivel = Clamp(motor.Nivel + IndustrialRandom.Range(-2.5, 2.5), 0, 100);
            motor.Eficiencia = Clamp(96 - (motor.Vibracion * 1.6) - Math.Max(0, motor.Temperatura - 82) * 0.45 + IndustrialRandom.Range(-2, 2), 70, 100);
            motor.ConsumoEnergia += IndustrialRandom.Range(0.2, 1.8);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
