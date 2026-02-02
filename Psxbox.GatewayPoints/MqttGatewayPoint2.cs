
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Psxbox.MQTTClient;
using System.Collections.Concurrent;

namespace Psxbox.GatewayPoint;

public class MqttGatewayPoint2(IConfiguration configuration, ILoggerFactory loggerFactory) : IGatewayPoint
{
    private bool disposedValue = false;
    private readonly ILogger logger = loggerFactory.CreateLogger<MqttGatewayPoint2>();

    public IGatewayPoint? BindedPoint { get; set; }
    public bool UseClientNamesList { get; set; } = true;
    public bool IsStarted { get; private set; } = false;

    public IEnumerable<string> ClientNamesList { get; set; } = [];
    private readonly ConcurrentDictionary<string, MqttManagedClient> mqttClients = [];

    #pragma warning disable CS0067, CS0414
    public event Func<(string, DateTimeOffset), Task>? OnClientConnected;
    public event Func<(string, DateTimeOffset), Task>? OnClientDisconnected;
    #pragma warning restore CS0067, CS0414

    public async Task SendMessageToClient(string clientName, byte[] data)
    {
        if (mqttClients.TryGetValue(clientName, out var client) && client.IsConnected)
        {
            await client.PublishAsync(clientName + "/up", data);
        }
    }

    public Task Start()
    {
        logger.LogInformation("Starting MQTT gateway point...");
        return StartMqttClients();
    }

    private async Task StartMqttClients()
    {
        foreach (var item in ClientNamesList)
        {
            await StartMqttClient(item);
        }

        IsStarted = true;
    }

    public async Task<bool> StartMqttClient(string item)
    {
        if (mqttClients.TryGetValue(item, out var client))
        {
            if (client.IsConnected) return true;

            await DisposeMqttClient(item);
        }

        MqttClientInfo clientInfo = new()
        {
            ClientId = item,
            Server = configuration["MqttBroker:Server"],
            Port = int.Parse(configuration["MqttBroker:Port"] ?? "0"),
            UserName = configuration["MqttBroker:UserName"],
            Password = configuration["MqttBroker:Password"],
        };

        var mqttClient = new MqttManagedClient(clientInfo, loggerFactory.CreateLogger($"MQTT CLIENT ({item})"));
        mqttClient.OnConnected += () =>
        {
            logger.LogInformation("Connected to broker");
            return Task.CompletedTask;
        };
        mqttClient.OnDisconnected += () =>
        {
            logger.LogInformation("Disconnected from broker");
            return Task.CompletedTask;
        };
        mqttClient.OnMessage += OnMessage;
        mqttClients[item] = mqttClient;

        await mqttClient.ConnectMqttClientAsync();
        await mqttClient.SubscribeAsync(item + "/down");
        return true;
    }

    private async Task OnMessage(string topic, byte[] data)
    {
        var clientName = topic.Split('/').First();
        logger.LogDebug("{clientName} > {data}", clientName, BitConverter.ToString(data));
        if (BindedPoint is null) return;
        await BindedPoint.SendMessageToClient(clientName, data);
    }

    public Task Stop()
    {
        logger.LogInformation("Stopping MQTT gateway point...");
        return StopMqttClients();
    }

    private async Task StopMqttClients()
    {
        foreach (var item in mqttClients.Keys)
        {
            await DisposeMqttClient(item);
        }
        IsStarted = false;
    }

    public async Task<bool> DisposeMqttClient(string item)
    {
        if (mqttClients.TryRemove(item, out var client))
        {
            if (client.IsConnected)
            {
                await client.UnsubscribeAsync(item + "/down");
                await client.DisconnectAsync();
            }
            client.OnMessage -= OnMessage;
            client.Dispose();
            return true;
        }
        return false;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Stop();

                BindedPoint = null;
                OnClientConnected = null;
                OnClientDisconnected = null;
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
