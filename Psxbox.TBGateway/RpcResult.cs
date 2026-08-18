namespace Psxbox.TBGateway;

/// <summary>
/// RPC metodining natijasi. <see cref="RawBody"/> berilgan bo'lsa javob tanasi
/// sifatida aynan u ishlatiladi — standart {success, message, data} konvertisiz.
/// Bu TB gateway-boshqaruv widget'i kutadigan shakllarni saqlash uchun kerak.
/// </summary>
internal readonly record struct RpcResult(bool Success, string Message, object? Data, object? RawBody)
{
    public const string DefaultMessage = "So`rovingiz muvaffaqiyatli bajarildi";

    public static RpcResult Ok(string? message = null, object? data = null)
        => new(true, message ?? DefaultMessage, data, null);

    public static RpcResult Raw(object body) => new(true, string.Empty, null, body);

    public static RpcResult Fail(string message) => new(false, message, null, null);
}
