using ENTITY.Models;
using System;

namespace BLL.Simulation
{
    public class SensorSimulator
    {
        public void Update(MotorTelemetry motor)
        {
            motor.Temperature = Clamp(motor.Temperature + IndustrialRandom.Range(-0.4, 1.1), 25, 120);
            motor.Pressure = IndustrialRandom.Range(80, 116);
            motor.Vibration = IndustrialRandom.Range(1, 5.8);
            motor.Voltage = IndustrialRandom.Range(208, 230);
            motor.Current = IndustrialRandom.Range(14, 36);
            motor.Torque = IndustrialRandom.Range(55, 95);
            motor.Level = Clamp(motor.Level + IndustrialRandom.Range(-2.5, 2.5), 0, 100);
            motor.Efficiency = IndustrialRandom.Range(80, 100);
            motor.EnergyConsumption += IndustrialRandom.Range(0.2, 1.8);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
