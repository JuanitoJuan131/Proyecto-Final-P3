using BLL.Mqtt;
using BLL.Persistence;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace BLL.Simulation
{
    public class SimulationManager
    {
        private readonly Dictionary<string, object> _tags = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly MqttTelemetryService _mqtt = new MqttTelemetryService();
        private readonly TelemetryPersistenceService _persistence = new TelemetryPersistenceService();
        private bool _mqttTelemetryActive;

        public SimulationManager()
        {
            Engine = new EngineSimulator();
            ConfigureMotors();
            Engine.Dispatcher.TagUpdated += OnSimulatorTagUpdated;
            _mqtt.MessageReceived += OnMqttMessageReceived;
            _mqtt.ConnectionChanged += (connected, message) => MqttConnectionChanged?.Invoke(connected, message);
            _persistence.PersistenceWarning += message => PersistenceWarning?.Invoke(message);
        }

        public EngineSimulator Engine { get; private set; }
        public MqttTelemetryService Mqtt { get { return _mqtt; } }

        public event Action<string, object> TagValueChanged;
        public event Action<bool, string> MqttConnectionChanged;
        public event Action<string, string> MqttMessageReceived;
        public event Action<string> PersistenceWarning;
        public event Action TagsCleared;

        public IReadOnlyDictionary<string, object> Tags
        {
            get { return _tags; }
        }

        public bool MqttTelemetryActive
        {
            get { return _mqttTelemetryActive; }
        }

        public void StartSimulation()
        {
            Engine.Start();
        }

        public void StopSimulation()
        {
            Engine.Stop();
        }

        public void ClearTags()
        {
            _tags.Clear();
            TagsCleared?.Invoke();
        }

        public void UseMqttTelemetry(bool enabled)
        {
            _mqttTelemetryActive = enabled;
        }

        public async Task ConnectMqttAsync(string server, int port, string topic)
        {
            await _mqtt.ConnectAsync(server, port);
            await _mqtt.SubscribeAsync(topic);
            UseMqttTelemetry(true);
        }

        public async Task DisconnectMqttAsync()
        {
            UseMqttTelemetry(false);
            await _mqtt.DisconnectAsync();
        }

        public void ShutdownMotor(string motorId)
        {
            Engine.ShutdownMotor(motorId);
        }

        public void RestartMotor(string motorId)
        {
            Engine.RestartMotor(motorId);
        }

        public object GetTagValue(string tag)
        {
            object value;
            return _tags.TryGetValue(tag, out value) ? value : null;
        }

        public double GetTagDouble(string tag)
        {
            var value = GetTagValue(tag);
            if (value == null)
            {
                return 0;
            }

            double number;
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out number)
                ? number
                : 0;
        }

        private void ConfigureMotors()
        {
            Engine.AddMotor(new TelemetriaMotor("MOTOR_01", "Lavadora de frutas", "Recepcion y lavado"));
            Engine.AddMotor(new TelemetriaMotor("MOTOR_02", "Extractor principal", "Extraccion"));
            Engine.AddMotor(new TelemetriaMotor("MOTOR_03", "Bomba de pulpa", "Filtrado"));
            Engine.AddMotor(new TelemetriaMotor("MOTOR_04", "Llenadora rotativa", "Envasado"));
        }

        private void OnSimulatorTagUpdated(string tag, object value)
        {
            if (_mqttTelemetryActive)
            {
                return;
            }

            UpdateTag(tag, value);
        }

        private void OnMqttMessageReceived(string topic, string payload)
        {
            MqttMessageReceived?.Invoke(topic, payload);

            var parts = topic.Split('/');
            if (parts.Length < 3)
            {
                return;
            }

            var tag = parts[1] + "." + parts[2];
            var normalizedPayload = payload.Replace(',', '.');
            double number;
            object value = double.TryParse(normalizedPayload, NumberStyles.Any, CultureInfo.InvariantCulture, out number)
                ? (object)number
                : payload;

            UpdateTag(tag, value);
        }

        private void UpdateTag(string tag, object value)
        {
            _tags[tag] = value;
            double numericValue;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out numericValue))
            {
                Task.Run(() => _persistence.PersistirLectura(tag, numericValue));
            }
            TagValueChanged?.Invoke(tag, value);
        }
    }
}
