using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Psxbox.MQTTClient;
using Psxbox.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Psxbox.TBGateway;

public partial class GatewayClient : IDisposable
{
    public const string GATEWAY_DEVICE_RPC_TOPIC = "v1/gateway/rpc";
    public const string GATEWAY_DEVICE_CONNECT_TOPIC = "v1/gateway/connect";
    public const string GATEWAY_DEVICE_DISCONNECT_TOPIC = "v1/gateway/disconnect";
    public const string GATEWAY_DEVICE_TELEMETRY_TOPIC = "v1/gateway/telemetry";
    public const string GATEWAY_DEVICE_ATTRIBUTES_TOPIC = "v1/gateway/attributes";
    public const string GATEWAY_DEVICE_ATTRIBUTES_REQUEST_TOPIC = "v1/gateway/attributes/request";
    public const string GATEWAY_DEVICE_ATTRIBUTES_RESPONSE_TOPIC = "v1/gateway/attributes/response";
    public const string DEVICE_TELEMETRY_TOPIC = "v1/devices/me/telemetry";
    public const string DEVICE_ATTRIBUTES_TOPIC = "v1/devices/me/attributes";
    public const string DEVICE_RPC_REQUEST_TOPIC = "v1/devices/me/rpc/request/";
    public const string DEVICE_RPC_RESPONSE_TOPIC = "v1/devices/me/rpc/response/";

    public const int ATTRIBUTE_REQUEST_TIMEOUT = 7000;

    private readonly MqttClientInfo _mqttClientInfo;
    private MqttAutoReconnectClient _mqttClient;
    private readonly ILogger? _logger;
    public event Action<string, byte[]>? OnMessage;

    public bool IsConnected { get; private set; } = false;

    public GatewayInfo GatewayInfo { get; private set; }

    private readonly ConcurrentDictionary<int, AttributeResponse> attributeResponses = new();
    private readonly object idLock = new();
    private int _attributeRequestId = 0;
    private bool disposedValue;

    public static GatewayClient GetGatewayClient(IConfiguration configuration, ILogger? logger = null)
    {
        GatewayInfo gateway = new()
        {
            Host = configuration["Gateway:Host"],
            Port = int.Parse(configuration["Gateway:Port"] ?? "0"),
            Token = configuration["Gateway:Token"],
        };

        return new GatewayClient(gateway, logger);
    }

    public GatewayClient(GatewayInfo gateway, ILogger? logger = null)
    {
        GatewayInfo = gateway;
        _logger = logger;
        _mqttClientInfo = GetClientInfo();
        _mqttClient = new MqttAutoReconnectClient(_mqttClientInfo, logger);
        _mqttClient.OnMessage += MqttClientOnMessageAsync;
        _mqttClient.OnConnected += MqttClientOnConnectedAsync;
        _mqttClient.OnDisconnected += MqttClientOnDisconnectedAsync;
    }

    private async Task MqttClientOnDisconnectedAsync()
    {
        IsConnected = false;
        await Task.CompletedTask;
    }

    private async Task MqttClientOnConnectedAsync()
    {
        IsConnected = true;
        await _mqttClient.SubscribeAsync(GATEWAY_DEVICE_RPC_TOPIC);
        await _mqttClient.SubscribeAsync(GATEWAY_DEVICE_ATTRIBUTES_RESPONSE_TOPIC);
        await _mqttClient.SubscribeAsync(DEVICE_RPC_REQUEST_TOPIC + "+");
    }

    private async Task MqttClientOnMessageAsync(string topic, byte[] msg)
    {
        // This is a gateway self RPC request
        if (topic.StartsWith(DEVICE_RPC_REQUEST_TOPIC))
            await RpcHandlerAsync(topic, msg);

        // Attribute response of the device created by gateway
        else if (topic.StartsWith(GATEWAY_DEVICE_ATTRIBUTES_RESPONSE_TOPIC))
            await AttributesResponseHandlerAsync(msg);

        // Device RPC request
        else if (topic.StartsWith(GATEWAY_DEVICE_RPC_TOPIC))
            await GatewayRpcHandlerAsync(topic, msg);

        OnMessage?.Invoke(topic, msg);
    }

    private MqttClientInfo GetClientInfo()
    {
        return new MqttClientInfo
        {
            UserName = GatewayInfo.Token,
            Password = string.Empty,
            Server = GatewayInfo.Host,
            Port = GatewayInfo.Port,
        };
    }

    public async Task<bool> StartAsync()
    {
        await _mqttClient.StartAsync();
        return _mqttClient.IsConnected;
    }

    public Task SubscribeAsync(string topic) => _mqttClient.SubscribeAsync(topic);

    public Task StopAsync() => _mqttClient.StopAsync();

    public void Update(GatewayInfo gateway)
    {
        GatewayInfo = gateway;
        MqttClientInfo clientInfo = GetClientInfo();
        if (clientInfo.IsEqual(_mqttClientInfo)) return;
        if (_mqttClient.IsConnected)
        {
            StopAsync().Wait(3000);
        }

        _mqttClient.Dispose();
        _mqttClient = new(_mqttClientInfo);
    }

    public Task UnsubscribeAsync(string topic)
    {
        return _mqttClient.UnsubscribeAsync(topic);
    }

    public Task ConnectDeviceAsync(string deviceId, string deviceType)
    {
        var payload = new
        {
            device = deviceId,
            type = deviceType,
        };

        return _mqttClient.PublishAsync(GATEWAY_DEVICE_CONNECT_TOPIC, JsonSerializer.Serialize(payload));
    }

    public Task DisconnectDeviceAsync(string deviceId)
    {
        var payload = new
        {
            device = deviceId,
        };

        return _mqttClient.PublishAsync(GATEWAY_DEVICE_DISCONNECT_TOPIC, JsonSerializer.Serialize(payload));
    }

    public Task SendTelemetryAsync(string payload, bool enqueue = false)
    {
        if (enqueue)
        {
            return EnqueueAsync(GATEWAY_DEVICE_TELEMETRY_TOPIC, payload);
        }
        return _mqttClient.PublishAsync(GATEWAY_DEVICE_TELEMETRY_TOPIC, payload);
    }

    public Task SendTelemetryAsync(string deviceName, List<object> data, bool enqueue = false)
    {
        var payloadDict = new Dictionary<string, List<object>>
        {
            [deviceName] = data
        };
        var payload = JsonSerializer.Serialize(payloadDict);
        return SendTelemetryAsync(payload, enqueue);
    }

    public Task EnqueueAsync(string topic, string? payload = null)
    {
        return _mqttClient.EnqueueMessageAsync(topic, payload ?? "");
    }

    public Task SendAttributesAsync(string payload, bool enqueue = false)
    {
        if (enqueue)
        {
            return EnqueueAsync(GATEWAY_DEVICE_ATTRIBUTES_TOPIC, payload);
        }
        return _mqttClient.PublishAsync(GATEWAY_DEVICE_ATTRIBUTES_TOPIC, payload);
    }

    public Task SendAttributesAsync(string deviceName, object data, bool enqueue = false)
    {
        var payload = new Dictionary<string, object>
        {
            [deviceName] = data
        };

        var payloadStr = JsonSerializer.Serialize(payload);
        return SendAttributesAsync(payloadStr, enqueue);
    }

    public Task SendAttributeAsync(string deviceName, string attributeKey, object value, bool enqueue = false)
    {
        var payload = new Dictionary<string, object>()
        {
            [$"{attributeKey}"] = value
        };
        return SendAttributesAsync(deviceName, payload, enqueue);
    }

    public Task<bool> WaitForConnect(int timeOut)
    {
        return _mqttClient.WaitForConnectedAsync(TimeSpan.FromMilliseconds(timeOut));
    }

    private async ValueTask<T?> RequestAttributeAsync<T>(string deviceName, string attributeKey,
        bool clientAttribute = true)
    {
        var requestId = GetNextRequestId();

        var payload = new
        {
            id = requestId,
            device = deviceName,
            client = clientAttribute,
            key = attributeKey
        };

        await _mqttClient.PublishAsync(GATEWAY_DEVICE_ATTRIBUTES_REQUEST_TOPIC, JsonSerializer.Serialize(payload));

        return await WaitForAttributeResponse<T>(deviceName, requestId, timeOut: ATTRIBUTE_REQUEST_TIMEOUT);
    }

    public async ValueTask<T?> GetAttribute<T>(string deviceName, string attributeKey, bool client = true)
    {
        try
        {
            return await RequestAttributeAsync<T>(deviceName, attributeKey, client);
        }
        catch (System.Exception ex)
        {
            _logger?.LogError(ex, "Error getting attribute: {ex}", ex.Message);
            return default;
        }
    }

    private async ValueTask<T?> WaitForAttributeResponse<T>(string deviceName, int requestId, int timeOut)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        while (true)
        {
            await Task.Yield();

            if (attributeResponses.TryRemove(requestId, out var response))
            {
                if (response.Device == deviceName)
                {
                    T? value = default;

                    if (response.Value.ValueKind != JsonValueKind.Undefined &&
                        response.Value.ValueKind != JsonValueKind.Null)
                    {
                        if (typeof(T) == typeof(string))
                        {
                            value = Converters.ConvertTo<T>(response.Value.ValueKind == JsonValueKind.String
                                ? response.Value.GetString()
                                : response.Value.GetRawText());
                        }
                        else
                        {
                            value = response.Value.Deserialize<T>();
                        }
                    }

                    return value;
                }
                else
                {
                    throw new Exception(
                        $"Attribute response device name mismatch. Expected: {deviceName}, Actual: {response.Device}");
                }
            }

            if (stopwatch.ElapsedMilliseconds > timeOut)
            {
                throw new Exception($"Attribute response timeout. RequestId: {requestId}");
            }

            await Task.Delay(100);
        }
    }

    private int GetNextRequestId()
    {
        lock (idLock)
        {
            Interlocked.Increment(ref _attributeRequestId);
            return _attributeRequestId;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                OnMessage = null;
                OnDeleteDevice = null;
                OnForceRead = null;
                OnGetDeviceInfo = null;
                OnNewDevice = null;
                OnRenameDevice = null;
                OnSetDeviceInfo = null;
                OnSetEnabled = null;

                attributeResponses.Clear();
                _mqttClient.Dispose();
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