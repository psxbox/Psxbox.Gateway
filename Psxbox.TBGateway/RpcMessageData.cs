namespace Psxbox.TBGateway;

public record RpcMessageData(int Id, string Method, Dictionary<string, object> Params);
