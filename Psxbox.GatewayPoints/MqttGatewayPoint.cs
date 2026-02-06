using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Psxbox.MQTTClient;

namespace Psxbox.GatewayPoint;

public class MqttGatewayPoint : IGatewayPoint
{
    public IGatewayPoint? BindedPoint { get; set; }
    public bool UseClientNamesList { get; set; }
    public IEnumerable<string> ClientNamesList { get; set; } = new List<string>();

    public bool IsStarted => _mqttManagedClient.IsConnected;

    private readonly ILogger _logger;
    private readonly MqttAutoReconnectClient _mqttManagedClient;

    #pragma warning disable CS0067
    public event Func<(string, DateTimeOffset), Task>? OnClientConnected;
    public event Func<(string, DateTimeOffset), Task>? OnClientDisconnected;
    #pragma warning restore CS0067

    public MqttGatewayPoint(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("MQTT CLIENT");

        MqttClientInfo mqttClientInfo = configuration.GetSection("MqttBroker").Get<MqttClientInfo>();

        _mqttManagedClient = new (mqttClientInfo, loggerFactory.CreateLogger<MqttAutoReconnectClient>());
        _mqttManagedClient.OnConnected += OnConnected;
        _mqttManagedClient.OnDisconnected += OnDisconnected;
        _mqttManagedClient.OnMessage += OnMessage;
    }

    private Task OnMessage(string topic, byte[] data)
    {

        var elements = topic.Split('/');
        var clientName = elements[0];

        _logger.LogTrace("{clientName} > {data}", clientName, BitConverter.ToString(data));

        if (BindedPoint is null) return Task.CompletedTask;

        return BindedPoint.SendMessageToClient(clientName, data);
    }

    private Task OnDisconnected()
    {
        _logger.LogInformation("Client is disconnected from broker");
        return Task.CompletedTask;
    }

    private Task OnConnected()
    {
        _logger.LogInformation("Client is connected to broker");
        return Subscribe();
    }

    public void Dispose()
    {
        _mqttManagedClient.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task SendMessageToClient(string clientName, byte[] data)
    {
        if (_mqttManagedClient.IsConnected)
        {
            return _mqttManagedClient.PublishAsync(clientName + "/up", data);
        }

        return Task.CompletedTask;
    }

    public Task Start()
    {
        if (!_mqttManagedClient.IsConnected)
            return _mqttManagedClient.StartAsync();

        return Task.CompletedTask;
    }

    public Task Unsubscribe(string clientName)
    {
        return Task.WhenAll(_mqttManagedClient.UnsubscribeAsync(clientName),
            _mqttManagedClient.UnsubscribeAsync(clientName + "/down"));
    }

    public Task Subscribe()
    {
        List<Task> tasks = [];

        if (UseClientNamesList)
        {
            tasks.Add(_mqttManagedClient.UnsubscribeAsync("+/down"));

            foreach (var item in ClientNamesList)
            {
                tasks.Add(_mqttManagedClient.SubscribeAsync($"{item}/down"));
            }
        }
        else
        {
            tasks.Add(_mqttManagedClient.SubscribeAsync("+/down"));
        }

        return Task.WhenAll(tasks);
    }

    public Task Stop()
    {
        return _mqttManagedClient.StopAsync();
    }
}
