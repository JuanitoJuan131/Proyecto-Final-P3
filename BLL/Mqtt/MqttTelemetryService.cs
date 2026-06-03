using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Mqtt
{
    public class MqttTelemetryService
    {
        private readonly SemaphoreSlim _connectionGate = new SemaphoreSlim(1, 1);
        private readonly HashSet<string> _activeSubscriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IMqttClient _client;
        private string _clientId;
        private bool _manualDisconnectRequested;

        public bool IsConnected
        {
            get { return _client != null && _client.IsConnected; }
        }

        public event Action<string, string> MessageReceived;
        public event Action<bool, string> ConnectionChanged;

        public async Task ConnectAsync(string server, int port)
        {
            await _connectionGate.WaitAsync();
            try
            {
                if (_client != null && _client.IsConnected)
                {
                    Trace.TraceInformation("MQTT already connected as {0}", _clientId);
                    ConnectionChanged?.Invoke(true, "Ya conectado");
                    return;
                }

                _manualDisconnectRequested = false;
                _activeSubscriptions.Clear();
                var factory = new MqttFactory();
                _client = factory.CreateMqttClient();
                _clientId = "VisualIoTDesktop-" + Environment.MachineName + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

                _client.UseConnectedHandler(e =>
                {
                    Trace.TraceInformation("MQTT connected as {0}", _clientId);
                    ConnectionChanged?.Invoke(true, "Conectado");
                });
                _client.UseDisconnectedHandler(e =>
                {
                    _activeSubscriptions.Clear();
                    var reason = _manualDisconnectRequested
                        ? "Desconectado manualmente"
                        : e.Exception == null ? "Desconectado" : e.Exception.Message;
                    Trace.TraceWarning("MQTT disconnected for client {0}: {1}", _clientId, reason);
                    ConnectionChanged?.Invoke(false, reason);
                });
                _client.UseApplicationMessageReceivedHandler(e =>
                {
                    var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? new byte[0]);
                    Trace.TraceInformation("MQTT message {0}: {1}", e.ApplicationMessage.Topic, payload);
                    MessageReceived?.Invoke(e.ApplicationMessage.Topic, payload);
                });

                var endpoints = await ResolveIpv4EndpointsAsync(server, port);
                Exception lastError = null;

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        Trace.TraceInformation("MQTT connecting to {0}:{1}", endpoint.Host, endpoint.Port);

                        var options = new MqttClientOptionsBuilder()
                            .WithClientId(_clientId)
                            .WithTcpServer(endpoint.Host, endpoint.Port)
                            .WithCleanSession()
                            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                            .WithCommunicationTimeout(TimeSpan.FromSeconds(10))
                            .Build();

                        await _client.ConnectAsync(options);
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        Trace.TraceError("MQTT connection error on {0}:{1}: {2}", endpoint.Host, endpoint.Port, ex);
                    }
                }

                var message = lastError == null
                    ? "No se pudo resolver un endpoint MQTT IPv4."
                    : lastError.Message;

                throw new InvalidOperationException("No se pudo conectar a MQTT por IPv4. " + message, lastError);
            }
            finally
            {
                _connectionGate.Release();
            }
        }

        public event Action<string> SubscriptionResult;

        public async Task SubscribeAsync(string topic)
        {
            if (!IsConnected)
            {
                Trace.TraceWarning("MQTT subscribe skipped because client is disconnected. Topic: {0}", topic);
                return;
            }

            if (string.IsNullOrWhiteSpace(topic))
            {
                Trace.TraceWarning("MQTT subscribe skipped because topic is empty.");
                return;
            }

            if (_activeSubscriptions.Contains(topic))
            {
                Trace.TraceInformation("MQTT subscription already active for {0}", topic);
                return;
            }

            Trace.TraceInformation("MQTT subscribing to {0}", topic);

            // Use explicit QoS 1 (at-least-once) and check result
            var options = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic).WithAtLeastOnceQoS())
                .Build();

            var result = await _client.SubscribeAsync(options);
            _activeSubscriptions.Add(topic);

            foreach (var item in result.Items)
            {
                var code = item.ResultCode.ToString();
                var msg = string.Format("Suscrito a [{0}] QoS1 -> codigo: {1}", item.TopicFilter.Topic, code);
                Trace.TraceInformation("MQTT {0}", msg);
                SubscriptionResult?.Invoke(msg);
            }
        }

        public async Task PublishAsync(string topic, object value)
        {
            if (!IsConnected)
            {
                return;
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Convert.ToString(value, CultureInfo.InvariantCulture))
                .Build();

            Trace.TraceInformation("MQTT publishing {0}: {1}", topic, value);
            await _client.PublishAsync(message);
        }

        public async Task DisconnectAsync()
        {
            await _connectionGate.WaitAsync();
            try
            {
                if (_client != null && _client.IsConnected)
                {
                    _manualDisconnectRequested = true;
                    Trace.TraceInformation("MQTT manual disconnect");
                    await _client.DisconnectAsync();
                }
                else
                {
                    _activeSubscriptions.Clear();
                    ConnectionChanged?.Invoke(false, "Desconectado manualmente");
                }
            }
            finally
            {
                _connectionGate.Release();
            }
        }

        private static async Task<List<MqttEndpoint>> ResolveIpv4EndpointsAsync(string server, int port)
        {
            IPAddress literalAddress;
            if (IPAddress.TryParse(server, out literalAddress))
            {
                return new List<MqttEndpoint>
                {
                    new MqttEndpoint(literalAddress.ToString(), port)
                };
            }

            var addresses = await Dns.GetHostAddressesAsync(server);
            var ipv4Addresses = addresses
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => new MqttEndpoint(address.ToString(), port))
                .ToList();

            if (ipv4Addresses.Count > 0)
            {
                return ipv4Addresses;
            }

            return new List<MqttEndpoint>
            {
                new MqttEndpoint(server, port)
            };
        }

        private class MqttEndpoint
        {
            public MqttEndpoint(string host, int port)
            {
                Host = host;
                Port = port;
            }

            public string Host { get; private set; }
            public int Port { get; private set; }
        }
    }
}
