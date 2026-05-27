using ENTITY.Models;

namespace BLL.Simulation
{
    public class AlarmManager
    {
        public void Evaluate(MotorTelemetry motor)
        {
            motor.AlarmActive = false;
            motor.AlarmMessage = string.Empty;

            if (motor.Temperature > 90)
            {
                Activate(motor, "Sobrecalentamiento");
            }
            else if (motor.Vibration > 5)
            {
                Activate(motor, "Vibracion excesiva");
            }
            else if (motor.Pressure > 115)
            {
                Activate(motor, "Sobrepresion");
            }
            else if (motor.Voltage < 210)
            {
                Activate(motor, "Bajo voltaje");
            }
            else if (motor.Efficiency < 88)
            {
                Activate(motor, "Baja eficiencia");
            }
        }

        private static void Activate(MotorTelemetry motor, string message)
        {
            motor.AlarmActive = true;
            motor.AlarmMessage = message;

            if (motor.State == MotorState.Running)
            {
                motor.State = MotorState.Warning;
            }
        }
    }
}
