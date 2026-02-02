using Microsoft.Extensions.Logging;
using SuperSimpleTcp;
using System.Collections.Concurrent;
using System.Text;

namespace Psxbox.GatewayPoint;

public sealed class TcpGatewayPoint(string ip, ushort port, ILoggerFactory? loggerFactory = null)
    : IGatewayPoint
{
    private SimpleTcpServer? _server;
    private readonly ConcurrentDictionary<string, string> _clientIpPorts = new();
    private readonly ConcurrentDictionary<string, string> _clientNames = new();
    private readonly ILogger? _logger = loggerFactory?.CreateLogger($"TCP SERVER ({ip}:{port})");
    private bool disposedValue;
    // private Timer? startTimer;

    public event Func<(string, DateTimeOffset), Task>? OnClientConnected;
    public event Func<(string, DateTimeOffset), Task>? OnClientDisconnected;

    public IGatewayPoint? BindedPoint { get; set; }
    public bool UseClientNamesList { get; set; } = false;
    public IEnumerable<string> ClientNamesList { get; set; } = new List<string>();

    public ushort Port { get; internal set; } = port;

    public bool IsStarted => _server?.IsListening ?? false;

    private void CreateServer(string ip, ushort port)
    {
        if (_server != null)
        {
            Stop();
        }

        _server = new SimpleTcpServer($"{ip}:{port}");
        _server.Events.ClientConnected += Server_ClientConnected;
        _server.Events.ClientDisconnected += Server_ClientDisconnected;
        _server.Events.DataReceived += Server_DataReceived;
        _server.Keepalive.EnableTcpKeepAlives = true;
        _server.Settings.IdleClientTimeoutMs = 0;
    }

    public IEnumerable<string> GetClientNames() => _clientNames.Keys;

    // public IEnumerable<string> GetClientIpPorts() => _server?.GetClients() ?? Array.Empty<string>();

    public async Task SendMessageToClient(string clientName, byte[] data)
    {
        if (_server != null && _clientNames.TryGetValue(clientName, out var ipPort))
        {
            await _server.SendAsync(ipPort, data);
        }
    }

    private async void Server_DataReceived(object? sender, DataReceivedEventArgs e)
    {
        try
        {
            _logger?.LogDebug("{client} > {data}", e.IpPort, BitConverter.ToString(e.Data.ToArray()));

            if (_clientIpPorts.TryGetValue(e.IpPort, out var clientName))
            {

                if (e.IpPort == clientName)
                {
                    string name = Encoding.ASCII.GetString(e.Data.ToArray());

                    if (UseClientNamesList && !ClientNamesList.Contains(name))
                    {
                        _server?.DisconnectClient(e.IpPort);
                        _logger?.LogWarning("The server use {param} and the name with {name} not found! IP and port: {ipPort}",
                            nameof(ClientNamesList), name, e.IpPort);
                        return;
                    }

                    _clientIpPorts[e.IpPort] = name;
                    if (_clientNames.TryGetValue(name, out var ipPort))
                    {
                        if (_server?.IsConnected(ipPort) == true)
                        {
                            _clientIpPorts[ipPort] = ipPort;
                            _logger?.LogWarning("Client with name {name} is registered. IP and port: {ipPort}", name, e.IpPort);
                        }
                        _clientNames[name] = e.IpPort;
                    }
                    else
                    {
                        _clientNames.TryAdd(name, e.IpPort);
                    }

                    _logger?.LogInformation("Client {ipPort} is registered with name {clientName}", e.IpPort, name);

                    OnClientConnected?.Invoke((name, DateTimeOffset.Now));
                }
                else
                {
                    if (BindedPoint is null) return;
                    await BindedPoint.SendMessageToClient(clientName, e.Data.ToArray());
                }
            }
            else
            {
                _server?.DisconnectClient(e.IpPort);
                _logger?.LogWarning("{ipPort} is not registered! Closing connection!", e.IpPort);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while server data received!");
        }
    }


    private void Server_ClientDisconnected(object? sender, ConnectionEventArgs e)
    {
        try
        {
            if (_clientIpPorts.Remove(e.IpPort, out var clientName))
            {
                _clientNames.Remove(clientName, out var _);
                _logger?.LogInformation("{client} is disconnected", clientName);

                OnClientDisconnected?.Invoke((clientName, DateTimeOffset.Now));
            }
            else
            {
                _logger?.LogInformation("{client} is disconnected", e.IpPort);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, ex.Message);
        }
    }


    private void Server_ClientConnected(object? sender, ConnectionEventArgs e)
    {
        try
        {
            if (_clientIpPorts.TryAdd(e.IpPort, e.IpPort))
            {
                _logger?.LogInformation("{client} is connected", e.IpPort);
            }
            else
            {
                _logger?.LogWarning("{client} is already exist! Updating info", e.IpPort);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while client connected!");
        }
    }

    private void Dispose(bool disposing)
    {
        if (disposedValue) return;

        if (disposing)
        {
            Stop();

            OnClientConnected = null;
            OnClientDisconnected = null;
        }

        disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async Task Start()
    {
        await Stop();
        CreateServer(ip, Port);

        _server!.Start();
        _logger?.LogInformation("Start listening");
    }

    public Task Stop()
    {
        // startTimer?.Dispose();

        _clientNames.Clear();
        _clientIpPorts.Clear();

        if (_server is not null)
        {
            _logger?.LogInformation("Stop listening");

            if (_server.IsListening) _server.Stop();

            _server.Events.ClientConnected -= Server_ClientConnected;
            _server.Events.ClientDisconnected -= Server_ClientDisconnected;
            _server.Events.DataReceived -= Server_DataReceived;
            _server.Dispose();
            _server = null;
            _logger?.LogInformation("Stopped");
        }

        return Task.CompletedTask;
    }
}
