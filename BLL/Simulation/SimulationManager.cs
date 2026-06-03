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
            _mqtt.SubscriptionResult += message => PersistenceWarning?.Invoke("[MQTT] " + message);
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

        public async Task PublishCommandAsync(string motorId, string accion)
        {
            if (!_mqtt.IsConnected || string.IsNullOrWhiteSpace(motorId) || string.IsNullOrWhiteSpace(accion))
                return;

            var topic = "visualiot/" + motorId + "/comandos";
            var payload = "{\"accion\":\"" + accion + "\"}";
            await _mqtt.PublishAsync(topic, payload);
            Trace.TraceInformation("Command sent to {0}: {1}", topic, payload);
        }

        /// <summary>
        /// Publica un payload JSON con valores variables que simulan exactamente lo que
        /// el ESP32 enviaría. Úsalo para verificar el pipeline o como modo demo.
        /// </summary>
        public async Task PublishSimulatedEsp32Async(string motorId)
        {
            motorId = string.IsNullOrWhiteSpace(motorId) ? "MOTOR_01" : motorId;
            var topic = "visualiot/" + motorId + "/datos";

            // Valores que varían con el tiempo para que el gauge y tendencia se animen
            var t = (DateTime.Now - DateTime.Today).TotalSeconds;
            var rpm      = (int)(1500 + Math.Sin(t / 8.0) * 900);            // 600–2400
            var temp     = Math.Round(52 + Math.Sin(t / 12.0) * 22, 1);      // 30–74 °C
            var corriente = Math.Round((rpm / 3000.0) * 30 + (Math.Sin(t / 3.0) * 1.5), 1);
            var voltaje   = Math.Round(380 + Math.Sin(t / 5.0) * 2, 1);
            var vibracion = Math.Round(0.5 + (rpm / 3000.0) * 4 + Math.Sin(t / 2.0) * 0.3, 2);
            var presion   = Math.Round(40 + (rpm / 3000.0) * 75, 1);
            var torque    = Math.Round(20 + (rpm / 3000.0) * 55, 1);
            var nivel     = Math.Round(55 + Math.Sin(t / 15.0) * 18, 1);
            var eficiencia = Math.Round(Math.Max(60, 100 - (temp - 25) * 0.4), 1);
            var horasOp   = (int)(t / 3600);

            var payload = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"rpm\":{0},\"temperatura\":{1},\"corriente\":{2},\"voltaje\":{3},"
                + "\"vibracion\":{4},\"presion\":{5},\"torque\":{6},\"nivel\":{7},"
                + "\"eficiencia\":{8},\"estado\":\"activo\",\"horas_op\":{9}}}",
                rpm, temp, corriente, voltaje, vibracion, presion, torque, nivel, eficiencia, horasOp);

            await _mqtt.PublishAsync(topic, payload);
            Trace.TraceInformation("Simulated ESP32 payload -> {0}: {1}", topic, payload);
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
            Engine.AddMotor(new TelemetriaMotor("TANQUE_01", "Tanque de lubricacion", "Lubricacion industrial"));
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

            var tag = NormalizeTag(parts[parts.Length - 2] + "." + parts[parts.Length - 1]);
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

                if (TryUpdateDirectJsonTag(values))
                {
                    return true;
                }

                var device = GetText(values, "device");
                if (string.IsNullOrWhiteSpace(device))
                {
                    device = ResolveDeviceFromTopic(topic);
                }

                if (string.IsNullOrWhiteSpace(device))
                {
                    Trace.TraceWarning("MQTT JSON message ignored without device. Topic: {0} Payload: {1}", topic, payload);
                    // Fallback: try MOTOR_01 if topic contains known namespace
                    if (topic.StartsWith("visualiot/", StringComparison.OrdinalIgnoreCase))
                    {
                        device = "MOTOR_01";
                        Trace.TraceInformation("MQTT: defaulting device to MOTOR_01 for visualiot topic");
                    }
                    else
                    {
                        return true;
                    }
                }

                device = NormalizeDeviceId(device);
                Trace.TraceInformation("MQTT telemetry mapped from {0} to device {1}", topic, device);
                UpdateMappedTag(device, "RPM", GetFirst(values, "rpm", "RPM"));
                UpdateMappedTag(device, "Temperatura", GetFirst(values, "temperatura", "temp", "temperature"));
                UpdateMappedTag(device, "Nivel", GetFirst(values, "nivelTanque", "nivel", "level"));
                UpdateMappedTag(device, "Presion", GetFirst(values, "presion", "pressure"));
                UpdateMappedTag(device, "Vibracion", GetFirst(values, "vibracion", "vibration"));
                UpdateMappedTag(device, "Corriente", GetFirst(values, "corriente", "current"));
                UpdateMappedTag(device, "Voltaje", GetFirst(values, "voltaje", "voltage"));
                UpdateMappedTag(device, "Eficiencia", GetFirst(values, "eficiencia", "efficiency"));
                UpdateMappedTag(device, "Potencia", GetFirst(values, "potencia", "power"));
                UpdateMappedTag(device, "Consumo", GetFirst(values, "consumo", "energy"));
                UpdateMappedTag(device, "Torque", GetFirst(values, "torque"));
                UpdateMappedTag(device, "Estado", NormalizeState(GetFirst(values, "estado", "state")));
                UpdateMappedTag(device, "HorasOp", GetFirst(values, "horas_op", "horasOp", "hours"));
                UpdateMappedTag(device, "Alarma", ResolveAlarmText(values));
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError("MQTT JSON parse error on {0}: {1}", topic, ex);
                return false;
            }
        }

        private bool TryUpdateDirectJsonTag(Dictionary<string, object> values)
        {
            var tag = GetText(values, "tag", "Tag", "sensor", "variable");
            var value = GetFirst(values, "value", "valor", "lectura", "reading");
            if (!string.IsNullOrWhiteSpace(tag) && value != null)
            {
                UpdateTag(NormalizeTag(tag), NormalizeTelemetryValue(value));
                return true;
            }

            var device = GetText(values, "device", "dispositivo", "motor");
            var metric = GetText(values, "metric", "metrica", "sensor", "variable");
            if (!string.IsNullOrWhiteSpace(device) && !string.IsNullOrWhiteSpace(metric) && value != null)
            {
                UpdateMappedTag(NormalizeDeviceId(device), NormalizeMetricName(metric), NormalizeTelemetryValue(value));
                return true;
            }

            return false;
        }

        private void UpdateMappedTag(string device, string metric, object value)
        {
            if (value == null)
            {
                return;
            }

            UpdateTag(NormalizeDeviceId(device) + "." + NormalizeMetricName(metric), NormalizeTelemetryValue(value));
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
            if (string.Equals(state, "activo", StringComparison.OrdinalIgnoreCase)) return "EnMarcha";
            if (string.Equals(state, "detenido", StringComparison.OrdinalIgnoreCase)) return "Apagado";
            if (string.Equals(state, "apagado", StringComparison.OrdinalIgnoreCase)) return "Apagado";
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

                var match = values.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match.Key))
                {
                    return match.Value;
                }
            }

            return null;
        }

        private static string GetText(Dictionary<string, object> values, params string[] keys)
        {
            return Convert.ToString(GetFirst(values, keys), CultureInfo.InvariantCulture);
        }

        private static string ResolveDeviceFromTopic(string topic)
        {
            var parts = string.IsNullOrWhiteSpace(topic)
                ? new string[0]
                : topic.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Scan all segments for motor/tanque identifiers (e.g. visualiot/MOTOR_01/datos)
            foreach (var part in parts)
            {
                var compact = part.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
                if (compact.StartsWith("motor") && compact.Length > 5)
                    return NormalizeDeviceId(part);
                if (compact.StartsWith("tanque") && compact.Length > 6)
                    return NormalizeDeviceId(part);
            }

            var last = parts.LastOrDefault();
            return string.IsNullOrWhiteSpace(last) ? string.Empty : NormalizeDeviceId(last);
        }

        private static string NormalizeDeviceId(string device)
        {
            device = (device ?? string.Empty).Trim();
            if (device.Equals("MOTOR_01_TANQUE", StringComparison.OrdinalIgnoreCase))
            {
                return "TANQUE_01";
            }

            var compact = device.Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace(":", string.Empty);
            if (compact.StartsWith("motor", StringComparison.OrdinalIgnoreCase) && compact.Length > 5)
            {
                return "MOTOR_" + compact.Substring(5).PadLeft(2, '0').ToUpperInvariant();
            }

            if (compact.StartsWith("tanque", StringComparison.OrdinalIgnoreCase) && compact.Length > 6)
            {
                return "TANQUE_" + compact.Substring(6).PadLeft(2, '0').ToUpperInvariant();
            }

            var separator = device.IndexOfAny(new[] { '-', ' ', ':' });
            if (separator > 0)
            {
                device = device.Substring(0, separator);
            }

            if (device.StartsWith("motor", StringComparison.OrdinalIgnoreCase) && device.IndexOf('_') < 0)
            {
                return "MOTOR_" + device.Substring(5).PadLeft(2, '0');
            }

            if (device.StartsWith("tanque", StringComparison.OrdinalIgnoreCase) && device.IndexOf('_') < 0)
            {
                return "TANQUE_" + device.Substring(6).PadLeft(2, '0');
            }

            return device.ToUpperInvariant();
        }

        private static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return string.Empty;
            }

            tag = tag.Trim().Replace('/', '.').Replace('\\', '.');
            var parts = tag.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return tag.ToUpperInvariant();
            }

            var device = NormalizeDeviceId(parts[parts.Length - 2]);
            var metric = NormalizeMetricName(parts[parts.Length - 1]);
            return device + "." + metric;
        }

        private static string NormalizeMetricName(string metric)
        {
            metric = (metric ?? string.Empty).Trim();
            if (metric.Length == 0)
            {
                return metric;
            }

            var key = metric.Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

            if (key == "rpm" || key == "velocidad") return "RPM";
            if (key == "temperatura" || key == "temp" || key == "temperature") return "Temperatura";
            if (key == "niveltanque" || key == "nivel" || key == "level") return "Nivel";
            if (key == "presion" || key == "pressure") return "Presion";
            if (key == "vibracion" || key == "vibration") return "Vibracion";
            if (key == "corriente" || key == "current" || key == "amperaje") return "Corriente";
            if (key == "voltaje" || key == "voltage") return "Voltaje";
            if (key == "eficiencia" || key == "efficiency") return "Eficiencia";
            if (key == "potencia" || key == "power") return "Potencia";
            if (key == "consumo" || key == "energy") return "Consumo";
            if (key == "torque") return "Torque";
            if (key == "estado" || key == "state") return "Estado";
            if (key == "alarma" || key == "alarm") return "Alarma";
            if (key == "horasop" || key == "horasoperacion" || key == "hours" || key == "horas") return "HorasOp";
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(metric.ToLowerInvariant());
        }

        private static object NormalizeTelemetryValue(object value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            double number;
            return double.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out number)
                ? (object)number
                : value;
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
