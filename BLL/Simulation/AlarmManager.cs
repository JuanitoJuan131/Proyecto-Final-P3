using ENTITY.Models;

namespace BLL.Simulation
{
    public class AlarmManager
    {
        public void Evaluate(TelemetriaMotor motor)
        {
            motor.AlarmaActiva = false;
            motor.MensajeAlarma = string.Empty;

            if (motor.Temperatura >= 98)
            {
                Activate(motor, "Falla critica: temperatura " + motor.Temperatura.ToString("0.0") + " C");
            }
            else if (motor.Vibracion >= 7.5)
            {
                Activate(motor, "Falla mecanica probable: vibracion " + motor.Vibracion.ToString("0.0") + " mm/s");
            }
            else if (motor.Corriente >= 42)
            {
                Activate(motor, "Sobrecarga electrica: corriente " + motor.Corriente.ToString("0.0") + " A");
            }
            else if (motor.Voltaje < 205 || motor.Voltaje > 235)
            {
                Activate(motor, "Voltaje fuera de rango: " + motor.Voltaje.ToString("0.0") + " V");
            }
            else if (motor.Presion > 118)
            {
                Activate(motor, "Sobrepresion en linea: " + motor.Presion.ToString("0.0") + " psi");
            }
            else if (motor.Temperatura >= 88 && motor.Corriente >= 34)
            {
                Activate(motor, "Alerta termica por carga elevada");
            }
            else if (motor.Vibracion >= 5.6 && motor.Rpm > 1650)
            {
                Activate(motor, "Desbalance o desgaste en rodamientos");
            }
            else if (motor.Eficiencia < 78 && motor.Corriente > 30)
            {
                Activate(motor, "Baja eficiencia con consumo elevado");
            }
        }

        private static void Activate(TelemetriaMotor motor, string message)
        {
            motor.AlarmaActiva = true;
            motor.MensajeAlarma = message;

            if (motor.Estado == EstadoMotor.EnMarcha)
            {
                motor.Estado = EstadoMotor.Advertencia;
            }
        }
    }
}
