
using Microsoft.Extensions.Logging;
using SuperSimpleTcp;
using System.Collections.Concurrent;

namespace Psxbox.GatewayPoint;

public sealed class Tcp2TcpGatewayPoint : IGatewayPoint
{
    private bool disposedValue;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<int, SimpleTcpServer> _servers = new();
    private readonly ConcurrentDictionary<int, string> _serverPorts = new();

    #pragma warning disable CS0067
    public event Func<(string, DateTimeOffset), Task>? OnClientConnected;
    public event Func<(string, DateTimeOffset), Task>? OnClientDisconnected;
    #pragma warning restore CS0067

    // public ConcurrentDictionary<int, (bool isStarted, IEnumerable<string> connectedClients)> ServerInfo => 
    //     _servers.ToDictionary(x => x.Key, x => (x.Value.IsListening, x.Value.GetClients()));
    // public Dictionary<int, string> RegisteredPorts => _serverPorts; 

    public IGatewayPoint? BindedPoint { get; set; }
    bool IGatewayPoint.UseClientNamesList { get; set; }
    public bool IsStarted { get; } = true;
    IEnumerable<string> IGatewayPoint.ClientNamesList { get; set; } = [];

    public Tcp2TcpGatewayPoint(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger($"TCP2TCP INTERNAL");
    }

    public Task SendMessageToClient(string clientName, byte[] data)
    {
        try
        {
            var serverPort = _serverPorts.FirstOrDefault(x => x.Value == clientName).Key;
            var server = _servers[serverPort]
                ?? throw new Exception($"Server with port {serverPort} and client name {clientName} not found");

            foreach (var ipPort in server.GetClients())
            {
                server.Send(ipPort, data);
            }
            _logger?.LogDebug("{client} < {data}", clientName, BitConverter.ToString(data));
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to send message to client {client}. Exception: {ex}", clientName, ex.Message);
        }

        return Task.CompletedTask;
    }

    public Task Start()
    {
        StartServers();
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        StopServers();
        return Task.CompletedTask;
    }

    public void RemovePort(int port)
    {
        StopServer(port);
        _serverPorts.TryRemove(port, out _);
    }

    public void RemovePorts(Dictionary<int, string> ports)
    {
        foreach (var port in ports)
        {
            RemovePort(port.Key);
        }
    }

    public void ClearPorts()
    {
        StopServers();
        _serverPorts.Clear();
    }

    public void StopServer(int port)
    {
        try
        {
            var server = _servers[port];
            if (server.IsListening)
            {
                server.Stop();
            }

            server.Events.ClientConnected -= Server_ClientConnected;
            server.Events.ClientDisconnected -= Server_ClientDisconnected;
            server.Events.DataReceived -= Server_DataReceived;

            server.Dispose();
            _servers.TryRemove(port, out _);
            _logger?.LogInformation("Stop listening on port {port}", port);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop server on port {port}", port);
        }
    }

    public void AddPorts(Dictionary<int, string> ports)
    {
        foreach (var port in ports)
        {
            _serverPorts.AddOrUpdate(port.Key, port.Value, (_, _) => port.Value);
        }
    }

    public void AddPort(int port, string clientName)
    {
        _serverPorts.AddOrUpdate(port, clientName, (_, _) => clientName);
    }

    public void StartServers(bool restart = false)
    {
        if (restart)
        {
            StopServers();
        }

        foreach (var port in _serverPorts)
        {
            StartServer(port.Key);
        }
    }

    public void StartServer(int port)
    {
        if (_servers.TryGetValue(port, out var server))
        {
            if (!server.IsListening)
            {
                server.Start();
            }
        }
        else
        {
            try
            {
                server = new SimpleTcpServer($"*:{port}");
                server.Events.ClientConnected += Server_ClientConnected;
                server.Events.ClientDisconnected += Server_ClientDisconnected;
                server.Events.DataReceived += Server_DataReceived;
                server.Keepalive.EnableTcpKeepAlives = true;
                server.Settings.IdleClientTimeoutMs = 0;
                server.Settings.MaxConnections = 2;
                server.Start();
                _servers.AddOrUpdate(port, server, (_, _) => server);
                _logger?.LogInformation("Start listening on port {port}", port);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while starting server");
            }
        }
    }

    public void StopServers()
    {
        var serverKeys = _servers.Keys.ToList();

        foreach (var key in serverKeys)
        {
            StopServer(key);
        }
    }

    private async void Server_DataReceived(object? sender, DataReceivedEventArgs e)
    {
        try
        {
            var server = (SimpleTcpServer)sender!;

            _logger?.LogDebug("{client} > {data}", server.Port, BitConverter.ToString(e.Data.ToArray()));
            await RouteMessage(server, e.Data.Array);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while processing data");
        }
    }

    private async Task RouteMessage(SimpleTcpServer server, byte[]? data)
    {
        if (data is null) return;
        if (BindedPoint is not null && _serverPorts.TryGetValue(server.Port, out var clientName))
        {
            await BindedPoint.SendMessageToClient(clientName, data);
        }
    }

    private void Server_ClientDisconnected(object? sender, ConnectionEventArgs e)
    {
        try
        {
            var server = (SimpleTcpServer?)sender;
            _logger?.LogInformation("[{port}]-{client} is disconnected", server?.Port, e.IpPort);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while processing data");
        }
    }

    private void Server_ClientConnected(object? sender, ConnectionEventArgs e)
    {
        try
        {
            var server = (SimpleTcpServer?)sender;
            _logger?.LogInformation("[{port}]-{client} is connected", server?.Port, e.IpPort);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while processing data");
        }
    }

    private void Dispose(bool disposing)
    {
        if (disposedValue) return;

        if (disposing)
        {
            StopServers();
        }

        disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
