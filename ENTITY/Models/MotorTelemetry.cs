using System;

namespace ENTITY.Models
{
    public enum MotorState
    {
        Off,
        Starting,
        Running,
        Warning,
        Fault,
        Stopping
    }

    public class MotorTelemetry
    {
        public MotorTelemetry(string id, string name, string line)
        {
            Id = id;
            Name = name;
            ProductionLine = line;
            Temperature = 25;
            Voltage = 220;
            Level = 50;
            Efficiency = 100;
            State = MotorState.Off;
            LastUpdate = DateTime.Now;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string ProductionLine { get; set; }
        public double Rpm { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public double Vibration { get; set; }
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Torque { get; set; }
        public double Level { get; set; }
        public double EnergyConsumption { get; set; }
        public double Efficiency { get; set; }
        public MotorState State { get; set; }
        public bool AlarmActive { get; set; }
        public string AlarmMessage { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}
