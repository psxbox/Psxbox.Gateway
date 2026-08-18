using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Psxbox.TBGateway;

public partial class GatewayClient
{
    /// <summary>
    /// New device event with (gateway client, device name, raw params).
    /// Handler qaytargan string RPC javob message sifatida TB ga boradi; null = default muvaffaqiyat xabari.
    /// </summary>
    public event Func<GatewayClient, string, JsonNode, Task<string?>>? OnNewDevice;

    /// <summary>
    /// Rename device event with (old device name, new device name).
    /// Handler qaytargan string RPC javob message sifatida TB ga boradi; null = default xabar.
    /// </summary>
    public event Func<string, string, Task<string?>>? OnRenameDevice;

    /// <summary>
    /// Delete device event with (device name).
    /// Handler qaytargan string RPC javob message sifatida TB ga boradi; null = default xabar.
    /// </summary>
    public event Func<string, Task<string?>>? OnDeleteDevice;

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
    /// Set device config event with (device name, device data).
    /// Handler qaytargan string RPC javob message sifatida TB ga boradi; null = default xabar.
    /// </summary>
    public event Func<string, object, Task<string?>>? OnSetDeviceInfo;

    /// <summary>
    /// v1/gateway/rpc — TB'da tanlangan bola qurilmaga yo'naltirilgan RPC.
    /// Bu yo'l faqat qurilma TB'da active bo'lganda yetadi.
    /// </summary>
    private async Task GatewayRpcHandlerAsync(string topic, byte[] msg)
    {
        var content = JsonNode.Parse(msg);
        var data = content?["data"] as JsonObject;
        if (content is null || data is null)
        {
            _logger?.LogWarning("Received null or empty content for topic: {Topic}", topic);
            return;
        }

        _logger?.LogInformation("GATEWAY DEVICE RPC -> Topic: {Topic}, Message: {Msg}", topic, content.ToJsonString());

        var request = new RpcRequest
        {
            Transport = RpcTransport.GatewayDevice,
            Method = data["method"]?.GetValue<string>()?.ToUpperInvariant() ?? string.Empty,
            Params = data["params"] as JsonObject ?? [],
            ResponseTopic = topic,
            TransportDeviceName = content["device"]?.GetValue<string>()
                                  ?? throw new Exception("Qurilma nomi berilmagan"),
            RequestId = data["id"]?.GetValue<int>() ?? throw new Exception("id berilmagan")
        };

        await HandleRpcAsync(request);
    }

    private static readonly JsonSerializerOptions AttributeResponseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private Task AttributesResponseHandlerAsync(byte[] msg)
    {
        var response = JsonSerializer.Deserialize<AttributeResponse>(msg, AttributeResponseJsonOptions);

        if (response == null) return Task.CompletedTask;

        if (attributeResponses.TryGetValue(response.Id, out var tcs))
            tcs.TrySetResult(response);

        return Task.CompletedTask;
    }

    /// <summary>
    /// v1/devices/me/rpc/request/{id} — gateway qurilmasining o'ziga yo'naltirilgan RPC.
    /// Gateway ulangan bo'lsa doim ishlaydi.
    /// </summary>
    private async Task RpcHandlerAsync(string topic, byte[] msg)
    {
        var requestId = topic[DEVICE_RPC_REQUEST_TOPIC.Length..];

        var node = JsonNode.Parse(msg);
        _logger?.LogInformation("GATEWAY RPC -> Topic: {Topic}, Message: {Msg}", topic, node?.ToJsonString());

        var request = new RpcRequest
        {
            Transport = RpcTransport.SelfDevice,
            Method = node?["method"]?.GetValue<string>()?.ToUpperInvariant() ?? string.Empty,
            // "as JsonObject": TB ba'zi ichki so'rovlarda (gateway_ping) params ni
            // obyekt emas, satr qilib jo'natadi ("params":"{}")
            Params = node?["params"] as JsonObject ?? [],
            ResponseTopic = DEVICE_RPC_RESPONSE_TOPIC + requestId
        };

        await HandleRpcAsync(request);
    }

    /// <summary>
    /// Ikkala transport uchun umumiy oqim: bajarish, xatoni ushlash, javobni yozish.
    /// </summary>
    private async Task HandleRpcAsync(RpcRequest request)
    {
        RpcResult result;
        try
        {
            var executed = await ExecuteRpcAsync(request);

            // Tanilmagan metod yoki ulanmagan handler — TB javob kutmaydi
            if (executed is null) return;

            result = executed.Value;
        }
        catch (Exception ex)
        {
            var message = $"Error on RPC method ({request.Method}): {ex.Message}";
            _logger?.LogError(ex, "{message}", message);
            result = RpcResult.Fail(message);
        }

        await EnqueueAsync(request.ResponseTopic, JsonSerializer.Serialize(BuildRpcResponse(request, result)));
    }

    /// <summary>
    /// Javob tanasi ikkala transportda bir xil; gateway yo'lida u qo'shimcha
    /// {device, id, data} konvertiga o'raladi.
    /// </summary>
    private static object BuildRpcResponse(RpcRequest request, RpcResult result)
    {
        var body = result.RawBody
                   ?? new { success = result.Success, message = result.Message, data = result.Data };

        return request.Transport == RpcTransport.GatewayDevice
            ? new { device = request.TransportDeviceName, id = request.RequestId, data = body }
            : body;
    }

    /// <summary>
    /// Yagona metod dispatcher'i — barcha metodlar ikkala transportda ham ochiq.
    /// <c>null</c> qaytarilsa javob yuborilmaydi.
    /// </summary>
    private async Task<RpcResult?> ExecuteRpcAsync(RpcRequest request)
    {
        switch (request.Method)
        {
            // Gateway-boshqaruv metodlari: TB widget'i aniq shaklni kutadi
            case "GATEWAY_PING":
                return RpcResult.Raw(new { success = true, message = "pong" });

            case "STATUS_GATEWAY":
            case "GATEWAY_STATS":
                return RpcResult.Raw(GatewayInfo);

            case "GATEWAY_VERSION":
                return RpcResult.Raw(new { version = GatewayVersion() });

            case "NEW_DEVICE" when OnNewDevice != null:
                return RpcResult.Ok(await OnNewDevice(this, request.DeviceName, request.Params));

            case "ENABLE_DEVICE" when OnSetEnabled != null:
                await OnSetEnabled(
                    request.DeviceName,
                    request.Params["enabled"]?.GetValue<bool>()
                        ?? throw new Exception("'enabled' parametri berilmagan"));
                return RpcResult.Ok();

            case "RENAME_DEVICE" when OnRenameDevice != null:
                return RpcResult.Ok(await OnRenameDevice(
                    request.DeviceName,
                    request.Params["newName"]?.GetValue<string>()
                        ?? throw new Exception("Yangi nom berilmagan")));

            case "DELETE_DEVICE" when OnDeleteDevice != null:
                return RpcResult.Ok(await OnDeleteDevice(request.DeviceName));

            case "SET_DEVICE_INFO" when OnSetDeviceInfo != null:
                return RpcResult.Ok(
                    await OnSetDeviceInfo(request.DeviceName, request.Params)
                    ?? "Ma'lumotlar o'zgartirildi");

            case "GET_DEVICE_INFO" when OnGetDeviceInfo != null:
                return RpcResult.Ok(data: await OnGetDeviceInfo(request.DeviceName));

            case "FORCE_READ" when OnForceRead != null:
                var archType = request.Params["arch_type"]?.GetValue<string>()?.ToUpperInvariant()
                               ?? throw new Exception("arch_type is null");
                await OnForceRead(request.DeviceName, archType, request.Payload);
                return RpcResult.Ok("So`rov qabul qilindi. Birozdan so`ng ma'lumotlar o`qib jo`natiladi");

            case "READ_DEVICE" when OnReadData != null:
            case "READ_DATA" when OnReadData != null:
                return RpcResult.Ok(
                    "So`rov qabul qilindi. Birozdan so`ng ma'lumotlar o`qib jo`natiladi",
                    await OnReadData(request.DeviceName, request.Payload));

            case "WRITE_DEVICE" when OnWriteData != null:
            case "WRITE_DATA" when OnWriteData != null:
                return RpcResult.Ok(
                    "So`rov qabul qilindi.",
                    await OnWriteData(request.DeviceName, request.Payload));

            default:
                return null;
        }
    }

    private string GatewayVersion()
    {
        var assembly = typeof(GatewayClient).Assembly;
        var versionAttribute = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                                       .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                                       .FirstOrDefault();
        return versionAttribute?.InformationalVersion ?? "unknown";
    }
}
