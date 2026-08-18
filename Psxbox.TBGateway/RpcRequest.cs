using System.Text.Json.Nodes;

namespace Psxbox.TBGateway;

internal enum RpcTransport
{
    /// <summary>v1/devices/me/rpc/request/{id} — gateway qurilmasining o'ziga.</summary>
    SelfDevice,

    /// <summary>v1/gateway/rpc — TB'da tanlangan bola qurilmaga, gateway orqali.</summary>
    GatewayDevice
}

/// <summary>
/// Ikkala TB RPC oqimining umumiy ko'rinishi. Oqimlar faqat transport qatlamida
/// farq qiladi (nom va yuk qayerdan olinadi, javob qanday o'raladi) — metodning
/// o'zi ikkalasida bir xil ishlaydi.
/// </summary>
internal sealed class RpcRequest
{
    public required RpcTransport Transport { get; init; }

    /// <summary>Katta harfga keltirilgan metod nomi.</summary>
    public required string Method { get; init; }

    /// <summary>Hech qachon null: "params" obyekt bo'lmasa bo'sh obyekt.</summary>
    public required JsonObject Params { get; init; }

    public required string ResponseTopic { get; init; }

    /// <summary>Gateway yo'lida konvertdagi "device"; self yo'lida null.</summary>
    public string? TransportDeviceName { get; init; }

    /// <summary>Gateway yo'lida "data.id"; self yo'lida null (u topic suffiksida).</summary>
    public int? RequestId { get; init; }

    /// <summary>
    /// Gateway yo'lida nom transport darajasida keladi; self yo'lida params ichida.
    /// NEW_DEVICE da "name" nuqta nomini bildiradi, shuning uchun u oxirgi fallback.
    /// </summary>
    public string DeviceName =>
        TransportDeviceName
        ?? Params["deviceName"]?.GetValue<string>()
        ?? Params["name"]?.GetValue<string>()
        ?? throw new Exception("Qurilma nomi berilmagan");

    /// <summary>
    /// Metodga uzatiladigan foydali yuk — reader sozlamalari bloki
    /// (DeviceScheduleMessage.Settings ga tushadi).
    /// Self-device yo'lida params qurilma nomini ham olib yurishi kerak, shuning uchun
    /// TB haqiqiy yukni "settings" ichiga o'raydi. Gateway yo'lida marshrutlash
    /// transport darajasida, params esa yukning o'zi.
    /// </summary>
    public JsonNode Payload => Params["settings"] ?? Params;
}
