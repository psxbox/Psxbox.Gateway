using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Psxbox.TBGateway;

public partial class GatewayClient
{
    /// <summary>
    /// New device event with (device name, client id, device id)
    /// </summary>
    public event Func<string, string, int?, Task>? OnNewDevice;

    /// <summary>
    /// Rename device event with (old device name, new device name)
    /// </summary>
    public event Func<string, string, Task>? OnRenameDevice;

    /// <summary>
    /// Delete device event with (device name)
    /// </summary>
    public event Func<string, Task>? OnDeleteDevice;

    /// <summary>
    /// Set enabled/disabled event with (device name, enabled)
    /// </summary>
    public event Func<string, bool, Task>? OnSetEnabled;

    /// <summary>
    /// Force read event with (device name, content type, additional data)
    /// </summary>
    public event Func<string, string, object?, Task>? OnForceRead;

    /// <summary>
    /// Read data event with (device name, parameters)
    /// </summary>
    public event Func<string, object, Task<object?>>? OnReadData;

    /// <summary>
    /// Write data event with (device name, data)
    /// </summary>
    public event Func<string, object, Task<object?>>? OnWriteData;

    /// <summary>
    /// Get device config event with (device name)
    /// </summary>
    public event Func<string, Task<Dictionary<string, object>>>? OnGetDeviceInfo;

    /// <summary>
    /// Set device config event with (device name, device data)
    /// </summary>
    public event Func<string, object, Task>? OnSetDeviceInfo;

    private async Task GatewayRpcHandlerAsync(string topic, byte[] msg)
    {
        var content = JsonNode.Parse(msg);
        var data = content?["data"];
        if (content == null || data == null)
        {
            _logger?.LogWarning("Received null or empty content for topic: {Topic}", topic);
            return;
        }

        _logger?.LogInformation("GATEWAY DEVICE RPC -> Topic: {Topic}, Message: {Msg}", topic, content.ToJsonString());

        var method = data["method"]?.GetValue<string>()?.ToUpper();

        var deviceName = content["device"]?.GetValue<string>() ?? throw new Exception("Qurilma nomi berilmagan");
        var requestId = data["id"]?.GetValue<int>() ?? throw new Exception("id berilmagan");
        var archType = data["params"]?["arch_type"]?.GetValue<string?>()?.ToUpper();

        bool success = true;
        string message = string.Empty;
        var result = new Dictionary<string, object>();
        object? resultData = null;

        try
        {
            switch (method)
            {
                case "GET_DEVICE_INFO" when OnGetDeviceInfo != null:
                    result = await OnGetDeviceInfo(deviceName);
                    break;
                case "SET_DEVICE_INFO" when OnSetDeviceInfo != null:
                    await OnSetDeviceInfo(deviceName, data["params"]!);
                    message = "Ma'lumotlar o'zgartirildi";
                    break;
                case "FORCE_READ" when OnForceRead != null:
                    if (archType is null) throw new Exception("arch_type is null");
                    await OnForceRead(deviceName, archType, data["params"]!);
                    message = "So`rov qabul qilindi. Birozdan so`ng ma'lumotlar o`qib jo`natiladi";
                    break;
                case "READ_DATA" when OnReadData != null:
                    resultData = await OnReadData(deviceName, data["params"]!);
                    message = "So`rov qabul qilindi. Birozdan so`ng ma'lumotlar o`qib jo`natiladi";
                    break;
                case "WRITE_DATA" when OnWriteData != null:
                    resultData = await OnWriteData(deviceName, data["params"]!);
                    message = "So`rov qabul qilindi.";
                    break;
                default:
                    return;
            }

            result["success"] = success;
            result["message"] = message;
            result["data"] = resultData ?? new();

            var res = new
            {
                device = deviceName,
                id = requestId,
                data = result,
            };
            await EnqueueAsync(topic, JsonSerializer.Serialize(res));
        }
        catch (Exception ex)
        {
            success = false;
            message = $"Error on RPC method ({method}): {ex.Message}";
            _logger?.LogError(ex, "{message}", message);

            result["success"] = success;
            result["message"] = message;
            var res = new
            {
                device = deviceName,
                id = requestId,
                data = result,
            };
            await EnqueueAsync(topic, JsonSerializer.Serialize(res));
        }
    }

    private Task AttributesResponseHandlerAsync(byte[] msg)
    {
        var response = JsonSerializer.Deserialize<AttributeResponse>(msg, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        if (response == null) return Task.CompletedTask;

        attributeResponses.AddOrUpdate(response.Id, response, (key, value) => response);
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// RPC handler for the Thingsboard Gateway
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="msg"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    private async Task RpcHandlerAsync(string topic, byte[] msg)
    {
        var requestId = topic[DEVICE_RPC_REQUEST_TOPIC.Length..];
        var responseTopic = DEVICE_RPC_RESPONSE_TOPIC + requestId;

        var node = JsonNode.Parse(msg);
        _logger?.LogInformation("GATEWAY RPC -> Topic: {Topic}, Message: {Msg}", topic, node?.ToJsonString());

        var method = node!["method"]?.GetValue<string>().ToUpper();
        string message;
        try
        {
            string? deviceName = node["params"]?["deviceName"]?.GetValue<string>();
            JsonNode? settings = node["params"]?["settings"];
            switch (method)
            {
                case "STATUS_GATEWAY":
                    await _mqttClient.PublishAsync(responseTopic, JsonSerializer.Serialize(GatewayInfo));
                    break;
                case "NEW_DEVICE" when OnNewDevice != null:
                    await NewDeviceHandler(node);
                    break;
                case "CONTROL_DEVICE" when OnSetEnabled != null:
                    await ControlDeviceHandler(node);
                    break;
                case "RENAME_DEVICE" when OnRenameDevice != null:
                    await RenameDeviceHandler(node);
                    break;
                case "DELETE_DEVICE" when OnDeleteDevice != null:
                    await DeleteDeviceHandler(node);
                    break;
                case "READ_DEVICE" when OnReadData != null:
                    _ = await OnReadData(
                        deviceName ?? throw new NullReferenceException("deviceName berilamagan"),
                        settings ?? throw new NullReferenceException("settings berilmagan"));
                    break;
                case "WRITE_DEVICE" when OnWriteData != null:
                    _ = await OnWriteData(
                        deviceName ?? throw new NullReferenceException("deviceName berilamagan"),
                        settings ?? throw new NullReferenceException("settings berilmagan"));
                    break;
                default:
                    return;
            }

            message = "So`rovingiz muvaffaqiyatli bajarildi";

            var res = new
            {
                success = true,
                message
            };
            await EnqueueAsync(responseTopic, JsonSerializer.Serialize(res));
        }
        catch (Exception ex)
        {
            message = $"Error on RPC method ({method}): {ex.Message}";
            _logger?.LogError(ex, "{message}", message);

            var res = new
            {
                success = false,
                message
            };
            await EnqueueAsync(responseTopic, JsonSerializer.Serialize(res));
        }
    }

    private Task NewDeviceHandler(JsonNode node)
    {
        var param = node!["params"] ?? throw new Exception("Parametrlar berilmadi");
        var deviceName = param!["name"] ?? throw new Exception("Qurilma nomi berilmagan");
        var clientId = param!["clientId"] ?? throw new Exception("clientId berilmagan");
        var deviceId = param!["deviceId"]?.GetValue<int>();

        if (OnNewDevice != null)
        {
            return OnNewDevice(deviceName.GetValue<string>(), clientId.GetValue<string>(), deviceId);
        }
        return Task.CompletedTask;
    }

    private Task ControlDeviceHandler(JsonNode node)
    {
        var param = node!["params"] ?? throw new Exception("Parametrlar berilmadi");
        var deviceName = param!["deviceName"]?.GetValue<string>() ?? throw new Exception("Qurilma nomi berilmagan");
        var enabled = param!["enabled"]?.GetValue<bool>() ?? throw new Exception("'enabled' parametri berilmagan");

        if (OnSetEnabled != null)
        {
            return OnSetEnabled(deviceName, enabled);
        }
        return Task.CompletedTask;
    }

    private Task RenameDeviceHandler(JsonNode node)
    {
        var param = node!["params"] ?? throw new Exception("Parametrlar berilmadi");
        var deviceName = param!["deviceName"]?.GetValue<string>() ?? throw new Exception("Qurilma nomi berilmagan");
        var newName = param!["newName"]?.GetValue<string>() ?? throw new Exception("Yangi nom berilmagan");
        if (OnRenameDevice != null)
        {
            return OnRenameDevice(deviceName, newName);
        }
        return Task.CompletedTask;
    }

    private Task DeleteDeviceHandler(JsonNode node)
    {
        var param = node["params"] ?? throw new Exception("Parametrlar berilmadi");
        var deviceName = param!["deviceName"]?.GetValue<string>() ?? throw new Exception("Qurilma nomi berilmagan");
        if (OnDeleteDevice != null)
        {
            return OnDeleteDevice(deviceName);
        }
        return Task.CompletedTask;
    }

}
