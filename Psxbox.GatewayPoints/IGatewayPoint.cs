namespace Psxbox.GatewayPoint;

public interface IGatewayPoint : IDisposable
{
    event Func<(string, DateTimeOffset), Task>? OnClientConnected;
    event Func<(string, DateTimeOffset), Task>? OnClientDisconnected;
    IGatewayPoint? BindedPoint { get; set; }
    bool UseClientNamesList { get; set; }
    bool IsStarted { get; }
    public IEnumerable<string> ClientNamesList { get; set; }

    Task SendMessageToClient(string clientName, byte[] data);

    Task Start();
    Task Stop();
}
