using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
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
        private IMqttClient _client;
        private string _clientId;

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
                    var reason = e.Exception == null ? "Desconectado" : e.Exception.Message;
                    Trace.TraceWarning("MQTT disconnected: {0}", reason);
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
                        ConnectionChanged?.Invoke(false, "Conectando a " + endpoint.Host + ":" + endpoint.Port);
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

        public async Task SubscribeAsync(string topic)
        {
            if (!IsConnected)
            {
                return;
            }

            Trace.TraceInformation("MQTT subscribing to {0}", topic);
            await _client.SubscribeAsync(topic);
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
                    Trace.TraceInformation("MQTT manual disconnect");
                    await _client.DisconnectAsync();
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
