using System.Text.Json;

namespace Psxbox.TBGateway
{
    public record AttributeResponse(int Id, string Device, JsonElement Value);
}
