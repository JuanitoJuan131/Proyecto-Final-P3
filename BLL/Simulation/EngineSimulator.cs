using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Simulation
{
    public class EngineSimulator
    {
        private readonly List<TelemetriaMotor> _motors = new List<TelemetriaMotor>();
        private readonly SensorSimulator _sensorSimulator = new SensorSimulator();
        private readonly AlarmManager _alarmManager = new AlarmManager();
        private readonly TagValueGenerator _tagValueGenerator = new TagValueGenerator();
        private CancellationTokenSource _cancellation;

        public TelemetryDispatcher Dispatcher { get; } = new TelemetryDispatcher();

        public IReadOnlyList<TelemetriaMotor> Motors
        {
            get { return _motors.AsReadOnly(); }
        }

        public void AddMotor(TelemetriaMotor motor)
        {
            _motors.Add(motor);
        }

        public void Start()
        {
            if (_cancellation != null && !_cancellation.IsCancellationRequested)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            Task.Run(() => RunAsync(_cancellation.Token), _cancellation.Token);
        }

        public void Stop()
        {
            if (_cancellation != null)
            {
                _cancellation.Cancel();
            }
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var motor in _motors)
                {
                    SimulateMotor(motor);
                    Dispatcher.Dispatch(_tagValueGenerator.Generate(motor));
                }

                await Task.Delay(500, token).ContinueWith(t => { });
            }
        }

        private void SimulateMotor(TelemetriaMotor motor)
        {
            switch (motor.Estado)
            {
                case EstadoMotor.Apagado:
                    if (IndustrialRandom.Chance(0.08))
                    {
                        motor.Estado = EstadoMotor.Arrancando;
                    }
                    break;

                case EstadoMotor.Arrancando:
                    motor.Rpm += IndustrialRandom.Range(120, 260);
                    motor.Corriente += IndustrialRandom.Range(6, 18);
                    motor.Temperatura += IndustrialRandom.Range(0.4, 1.4);
                    if (motor.Rpm >= 1720)
                    {
                        motor.Rpm = 1720;
                        motor.Estado = EstadoMotor.EnMarcha;
                    }
                    break;

                case EstadoMotor.EnMarcha:
                case EstadoMotor.Advertencia:
                    motor.Rpm = Clamp(motor.Rpm + IndustrialRandom.Range(-25, 25), 1620, 1820);
                    _sensorSimulator.Update(motor);
                    _alarmManager.Evaluate(motor);

                    if (IndustrialRandom.Chance(0.0015))
                    {
                        motor.Estado = EstadoMotor.Falla;
                    }
                    else if (!motor.AlarmaActiva)
                    {
                        motor.Estado = EstadoMotor.EnMarcha;
                    }
                    break;

                case EstadoMotor.Falla:
                    motor.Rpm = Clamp(motor.Rpm - IndustrialRandom.Range(120, 260), 0, 1800);
                    motor.Vibracion = IndustrialRandom.Range(8, 15);
                    motor.Temperatura = Clamp(motor.Temperatura + IndustrialRandom.Range(0.8, 2.2), 25, 120);
                    motor.AlarmaActiva = true;
                    motor.MensajeAlarma = "Falla critica de motor";
                    if (motor.Rpm <= 0)
                    {
                        motor.Estado = EstadoMotor.Apagado;
                    }
                    break;

                case EstadoMotor.Deteniendo:
                    motor.Rpm = Clamp(motor.Rpm - IndustrialRandom.Range(80, 180), 0, 1800);
                    motor.Corriente = Clamp(motor.Corriente - IndustrialRandom.Range(3, 8), 0, 40);
                    if (motor.Rpm <= 0)
                    {
                        motor.Estado = EstadoMotor.Apagado;
                    }
                    break;
            }

            motor.UltimaActualizacion = DateTime.Now;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
