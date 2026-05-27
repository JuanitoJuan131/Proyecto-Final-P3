using ENTITY.Models;
using System.Collections.Generic;

namespace BLL.Simulation
{
    public class TagValueGenerator
    {
        public Dictionary<string, object> Generate(MotorTelemetry motor)
        {
            return new Dictionary<string, object>
            {
                { motor.Id + ".RPM", motor.Rpm },
                { motor.Id + ".Temperatura", motor.Temperature },
                { motor.Id + ".Presion", motor.Pressure },
                { motor.Id + ".Vibracion", motor.Vibration },
                { motor.Id + ".Voltaje", motor.Voltage },
                { motor.Id + ".Corriente", motor.Current },
                { motor.Id + ".Torque", motor.Torque },
                { motor.Id + ".Nivel", motor.Level },
                { motor.Id + ".Eficiencia", motor.Efficiency },
                { motor.Id + ".Estado", motor.State.ToString() },
                { motor.Id + ".Alarma", motor.AlarmMessage }
            };
        }
    }
}
