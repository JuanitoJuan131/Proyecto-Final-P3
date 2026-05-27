using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Mqtt
{
    public class MqttTelemetryService
    {
        private IMqttClient _client;

        public bool IsConnected
        {
            get { return _client != null && _client.IsConnected; }
        }

        public event Action<string, string> MessageReceived;
        public event Action<bool, string> ConnectionChanged;

        public async Task ConnectAsync(string server, int port)
        {
            if (_client != null && _client.IsConnected)
            {
                await _client.DisconnectAsync();
            }

            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _client.UseConnectedHandler(e => ConnectionChanged?.Invoke(true, "Conectado"));
            _client.UseDisconnectedHandler(e => ConnectionChanged?.Invoke(false, "Desconectado"));
            _client.UseApplicationMessageReceivedHandler(e =>
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? new byte[0]);
                MessageReceived?.Invoke(e.ApplicationMessage.Topic, payload);
            });

            var endpoints = await ResolveIpv4EndpointsAsync(server, port);
            Exception lastError = null;

            foreach (var endpoint in endpoints)
            {
                try
                {
                    ConnectionChanged?.Invoke(false, "Conectando a " + endpoint.Host + ":" + endpoint.Port);

                    var options = new MqttClientOptionsBuilder()
                        .WithClientId("VisualIoTDesktop-" + Guid.NewGuid().ToString("N"))
                        .WithTcpServer(endpoint.Host, endpoint.Port)
                        .WithCleanSession()
                        .WithCommunicationTimeout(TimeSpan.FromSeconds(7))
                        .Build();

                    await _client.ConnectAsync(options);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            var message = lastError == null
                ? "No se pudo resolver un endpoint MQTT IPv4."
                : lastError.Message;

            throw new InvalidOperationException("No se pudo conectar a MQTT por IPv4. " + message, lastError);
        }

        public async Task SubscribeAsync(string topic)
        {
            if (!IsConnected)
            {
                return;
            }

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

            await _client.PublishAsync(message);
        }

        public async Task DisconnectAsync()
        {
            if (_client != null && _client.IsConnected)
            {
                await _client.DisconnectAsync();
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
