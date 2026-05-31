using BLL.Mqtt;
using BLL.Persistence;
using ENTITY.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BLL.Simulation
{
    public class SimulationManager
    {
        private readonly Dictionary<string, object> _tags = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly MqttTelemetryService _mqtt = new MqttTelemetryService();
        private readonly TelemetryPersistenceService _persistence = new TelemetryPersistenceService();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
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
        public event Action<string> MotorTagsCleared;

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

        public void ClearMotorTags(string motorId)
        {
            if (string.IsNullOrWhiteSpace(motorId))
            {
                return;
            }

            var prefix = motorId + ".";
            foreach (var tag in _tags.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _tags.Remove(tag);
            }

            MotorTagsCleared?.Invoke(motorId);
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

            if (TryUpdateJsonTelemetry(topic, payload))
            {
                return;
            }

            var parts = topic.Split('/');
            if (parts.Length < 3)
            {
                Trace.TraceWarning("MQTT message ignored because topic has no tag metric: {0}", topic);
                return;
            }

            var tag = parts[parts.Length - 2] + "." + parts[parts.Length - 1];
            var normalizedPayload = payload.Replace(',', '.');
            double number;
            object value = double.TryParse(normalizedPayload, NumberStyles.Any, CultureInfo.InvariantCulture, out number)
                ? (object)number
                : payload;

            UpdateTag(tag, value);
        }

        private bool TryUpdateJsonTelemetry(string topic, string payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || !payload.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var values = _json.Deserialize<Dictionary<string, object>>(payload);
                if (values == null)
                {
                    return false;
                }

                var device = GetText(values, "device");
                if (string.IsNullOrWhiteSpace(device))
                {
                    device = ResolveDeviceFromTopic(topic);
                }

                if (string.IsNullOrWhiteSpace(device))
                {
                    Trace.TraceWarning("MQTT JSON message ignored without device: {0}", payload);
                    return true;
                }

                device = NormalizeDeviceId(device);
                UpdateMappedTag(device, "RPM", GetFirst(values, "rpm", "RPM"));
                UpdateMappedTag(device, "Temperatura", GetFirst(values, "temperatura", "temp", "temperature"));
                UpdateMappedTag(device, "Nivel", GetFirst(values, "nivelTanque", "nivel", "level"));
                UpdateMappedTag(device, "Estado", NormalizeState(GetFirst(values, "estado", "state")));
                UpdateMappedTag(device, "Alarma", ResolveAlarmText(values));
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError("MQTT JSON parse error on {0}: {1}", topic, ex);
                return false;
            }
        }

        private void UpdateMappedTag(string device, string metric, object value)
        {
            if (value == null)
            {
                return;
            }

            UpdateTag(device + "." + metric, value);
        }

        private static object ResolveAlarmText(Dictionary<string, object> values)
        {
            var alarm = GetFirst(values, "alarma", "alarm");
            var active = false;
            if (alarm is bool)
            {
                active = (bool)alarm;
            }
            else if (alarm != null)
            {
                bool.TryParse(Convert.ToString(alarm, CultureInfo.InvariantCulture), out active);
            }

            if (!active)
            {
                return "Sin alarmas";
            }

            var state = Convert.ToString(GetFirst(values, "estado", "state"), CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(state) ? "Alarma activa" : "Alarma activa: " + NormalizeState(state);
        }

        private static object NormalizeState(object value)
        {
            var state = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.Equals(state, "RUNNING", StringComparison.OrdinalIgnoreCase)) return "EnMarcha";
            if (string.Equals(state, "WARNING", StringComparison.OrdinalIgnoreCase)) return "Advertencia";
            if (string.Equals(state, "FAULT", StringComparison.OrdinalIgnoreCase)) return "Falla";
            return value;
        }

        private static object GetFirst(Dictionary<string, object> values, params string[] keys)
        {
            foreach (var key in keys)
            {
                object value;
                if (values.TryGetValue(key, out value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string GetText(Dictionary<string, object> values, string key)
        {
            return Convert.ToString(GetFirst(values, key), CultureInfo.InvariantCulture);
        }

        private static string ResolveDeviceFromTopic(string topic)
        {
            var last = string.IsNullOrWhiteSpace(topic) ? string.Empty : topic.Split('/').LastOrDefault();
            if (string.IsNullOrWhiteSpace(last))
            {
                return string.Empty;
            }

            return last.StartsWith("motor", StringComparison.OrdinalIgnoreCase)
                ? "MOTOR_" + last.Substring(5).PadLeft(2, '0')
                : last.ToUpperInvariant();
        }

        private static string NormalizeDeviceId(string device)
        {
            device = (device ?? string.Empty).Trim();
            if (device.StartsWith("motor", StringComparison.OrdinalIgnoreCase) && device.IndexOf('_') < 0)
            {
                return "MOTOR_" + device.Substring(5).PadLeft(2, '0');
            }

            return device.ToUpperInvariant();
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
