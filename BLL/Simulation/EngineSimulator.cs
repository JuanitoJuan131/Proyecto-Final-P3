using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Simulation
{
    public class EngineSimulator
    {
        private readonly List<MotorTelemetry> _motors = new List<MotorTelemetry>();
        private readonly SensorSimulator _sensorSimulator = new SensorSimulator();
        private readonly AlarmManager _alarmManager = new AlarmManager();
        private readonly TagValueGenerator _tagValueGenerator = new TagValueGenerator();
        private CancellationTokenSource _cancellation;

        public TelemetryDispatcher Dispatcher { get; } = new TelemetryDispatcher();

        public IReadOnlyList<MotorTelemetry> Motors
        {
            get { return _motors.AsReadOnly(); }
        }

        public void AddMotor(MotorTelemetry motor)
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

        private void SimulateMotor(MotorTelemetry motor)
        {
            switch (motor.State)
            {
                case MotorState.Off:
                    if (IndustrialRandom.Chance(0.08))
                    {
                        motor.State = MotorState.Starting;
                    }
                    break;

                case MotorState.Starting:
                    motor.Rpm += IndustrialRandom.Range(120, 260);
                    motor.Current += IndustrialRandom.Range(6, 18);
                    motor.Temperature += IndustrialRandom.Range(0.4, 1.4);
                    if (motor.Rpm >= 1720)
                    {
                        motor.Rpm = 1720;
                        motor.State = MotorState.Running;
                    }
                    break;

                case MotorState.Running:
                case MotorState.Warning:
                    motor.Rpm = Clamp(motor.Rpm + IndustrialRandom.Range(-25, 25), 1620, 1820);
                    _sensorSimulator.Update(motor);
                    _alarmManager.Evaluate(motor);

                    if (IndustrialRandom.Chance(0.002))
                    {
                        motor.State = MotorState.Fault;
                    }
                    else if (!motor.AlarmActive)
                    {
                        motor.State = MotorState.Running;
                    }
                    break;

                case MotorState.Fault:
                    motor.Rpm = Clamp(motor.Rpm - IndustrialRandom.Range(120, 260), 0, 1800);
                    motor.Vibration = IndustrialRandom.Range(8, 15);
                    motor.Temperature = Clamp(motor.Temperature + IndustrialRandom.Range(0.8, 2.2), 25, 120);
                    motor.AlarmActive = true;
                    motor.AlarmMessage = "Falla critica de motor";
                    if (motor.Rpm <= 0)
                    {
                        motor.State = MotorState.Off;
                    }
                    break;

                case MotorState.Stopping:
                    motor.Rpm = Clamp(motor.Rpm - IndustrialRandom.Range(80, 180), 0, 1800);
                    motor.Current = Clamp(motor.Current - IndustrialRandom.Range(3, 8), 0, 40);
                    if (motor.Rpm <= 0)
                    {
                        motor.State = MotorState.Off;
                    }
                    break;
            }

            motor.LastUpdate = DateTime.Now;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
