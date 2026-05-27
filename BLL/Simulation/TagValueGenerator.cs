using ENTITY.Models;
using System.Collections.Generic;

namespace BLL.Simulation
{
    public class TagValueGenerator
    {
        public Dictionary<string, object> Generate(TelemetriaMotor motor)
        {
            return new Dictionary<string, object>
            {
                { motor.Id + ".RPM", motor.Rpm },
                { motor.Id + ".Temperatura", motor.Temperatura },
                { motor.Id + ".Presion", motor.Presion },
                { motor.Id + ".Vibracion", motor.Vibracion },
                { motor.Id + ".Voltaje", motor.Voltaje },
                { motor.Id + ".Corriente", motor.Corriente },
                { motor.Id + ".Torque", motor.Torque },
                { motor.Id + ".Nivel", motor.Nivel },
                { motor.Id + ".Eficiencia", motor.Eficiencia },
                { motor.Id + ".Estado", motor.Estado.ToString() },
                { motor.Id + ".Alarma", motor.MensajeAlarma }
            };
        }
    }
}
