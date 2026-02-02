
using Microsoft.Extensions.Logging;
using SuperSimpleTcp;
using System.Collections.Concurrent;
using System.Text;

namespace Psxbox.GatewayPoint;

public sealed class CollectorModemPoint(string ip, ushort port, ILoggerFactory? loggerFactory = null) : IGatewayPoint
{
    private SimpleTcpServer? _server;
    private Timer? startTimer;
    private bool disposedValue;
    private readonly ConcurrentDictionary<string, CollectorNode> _clientIpPorts = new();

    public IGatewayPoint? BindedPoint { get; set; }
    public bool UseClientNamesList { get; set; } = true;
    private readonly ILogger<CollectorModemPoint>? _logger = loggerFactory?.CreateLogger<CollectorModemPoint>();
    public bool IsStarted => _server is not null && _server.IsListening;

    public IEnumerable<string> ClientNamesList { get; set; } = [];
    public IEnumerable<string>? ConnectedClients => _clientIpPorts.Values.Select(x => x.IMEI);
    public event Func<(string, DateTimeOffset), Task>? OnClientConnected;
    public event Func<(string, DateTimeOffset), Task>? OnClientDisconnected;

    public async Task SendMessageToClient(string clientName, byte[] data)
    {
        var ipPort = _clientIpPorts.FirstOrDefault(x => x.Value.IMEI == clientName);

        if (_server != null && ipPort.Value != default)
        {
            byte[] payload = GetPayload(client: clientName, func: 0x01, data: data);
            await _server.SendAsync(ipPort.Key, payload);
        }
    }

    private static byte[] GetPayload(string client, byte func, byte[] data)
    {
        byte star = (byte)'*';
        int dataLength = data.Length;

        return [star, .. Encoding.ASCII.GetBytes(client), star, func, star, (byte)(dataLength / 256), (byte)(dataLength % 256), star, .. data];
    }

    public Task Start()
    {
        Stop();
        CreateServer(ip, port);

        startTimer = new Timer(CheckStarted, null, 1000, 3000);

        return Task.CompletedTask;
    }

    private void CheckStarted(object? state)
    {
        if (_server is not null && !_server.IsListening)
        {
            _server?.Start();
            if (_server?.IsListening ?? false)
                _logger?.LogInformation("Start listening on {ip}:{port}", ip, port);
        }
    }

    private void CreateServer(object ip, object port)
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

    private async void Server_DataReceived(object? sender, DataReceivedEventArgs e)
    {
        try
        {
            var data = e.Data.ToArray();
            string cl = e.IpPort;

            if (_clientIpPorts.TryGetValue(e.IpPort, out var node))
            {
                cl = node.IMEI;
            }
            _logger?.LogDebug("{client} > {data}", cl, BitConverter.ToString(data));

            if (!ParsePayload(data, out string imei, out byte func, out byte[] payload))
            {
                _logger?.LogDebug("Invalid payload from {client} > {data}", e.IpPort, BitConverter.ToString(data.Take(50).ToArray()));
                return;
            }

            if (UseClientNamesList && !ClientNamesList.Contains(imei))
            {
                _server?.DisconnectClient(e.IpPort);
                _logger?.LogWarning("The server use {param} and the name with {name} not found!",
                    nameof(ClientNamesList), imei);
                return;
            }

            if (node == null)
            {
                node = new CollectorNode(e.IpPort, imei);
                node.OnTime += Node_OnTime;
            }
            else
            {
                node.IMEI = imei;
            }

            _clientIpPorts[e.IpPort] = node;

            switch (func)
            {
                case 0x01:
                    {
                        node.RestartTimer();

                        if (BindedPoint is null) return;
                        await BindedPoint.SendMessageToClient(imei, payload);
                        _logger?.LogDebug("Message for binded point {client} is {data}", imei, BitConverter.ToString(payload));
                        break;
                    }
                case 0x02:
                    {
                        node.RestartTimer();

                        OnClientConnected?.Invoke((imei, DateTimeOffset.Now));

                        var response = GetPayload(imei, 0x03, payload);
                        await _server!.SendAsync(e.IpPort, response);
                        _logger?.LogDebug("Response for {client} is {response}", imei, BitConverter.ToString(response));
                        break;
                    }
                case 0x04:
                    {
                        node.RestartTimer();

                        OnClientConnected?.Invoke((imei, DateTimeOffset.Now));

                        var response = GetPayload(imei, 0x05, payload);
                        await _server!.SendAsync(e.IpPort, response);
                        _logger?.LogDebug("Response for {client} is {response}", imei, BitConverter.ToString(response));
                        break;
                    }
                default:
                    _logger?.LogError("Unknown function code {func} from {client}", func, e.IpPort);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while processing data");
        }
    }

    private Task Node_OnTime(string ipPort)
    {
        _server?.DisconnectClient(ipPort);
        return Task.CompletedTask;
    }

    private static bool ParsePayload(byte[] bytes, out string imei, out byte func, out byte[] data)
    {
        try
        {
            var star = (byte)'*';
            var starIndex = Array.IndexOf(bytes, star);

            if (bytes.Length < 22 || bytes[starIndex + 16] != star || bytes[starIndex + 18] != star)
                throw new InvalidDataException();

            var imeiBytes = bytes[(starIndex + 1)..(starIndex + 16)];
            imei = Encoding.ASCII.GetString(imeiBytes.ToArray()).Trim();
            ThrowIfInvalidImei(imei);
            func = bytes[starIndex + 17];
            var len = bytes[starIndex + 19] * 256 + bytes[starIndex + 20];
            if (len > 0 && bytes.Length < (starIndex + 21 + len))
            {
                data = [];
                return false;
            }
            data = len > 0 ? bytes[(starIndex + 22)..].ToArray() : [];

            return true;
        }
        catch (InvalidDataException)
        {
            imei = "";
            func = 0;
            data = [];
            return false;
        }
    }

    private static void ThrowIfInvalidImei(string imei)
    {
        if (imei.Length != 15 || !imei.All(char.IsDigit))
        {
            throw new Exception($"Invalid IMEI: {imei}");
        }
    }

    private void Server_ClientDisconnected(object? sender, ConnectionEventArgs e)
    {
        try
        {
            if (_clientIpPorts.TryRemove(e.IpPort, out var node))
            {
                _logger?.LogInformation("{client} is disconnected", node);

                if (e.IpPort != node.IMEI)
                    OnClientDisconnected?.Invoke((node.IMEI, DateTimeOffset.Now));

                node.OnTime -= Node_OnTime;
                node.Dispose();
            }
            else
            {
                _logger?.LogInformation("{client} is disconnected", e.IpPort);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error on client disconnected");
        }
    }

    private void Server_ClientConnected(object? sender, ConnectionEventArgs e)
    {
        try
        {
            var node = new CollectorNode(e.IpPort, e.IpPort);
            node.OnTime += Node_OnTime;

            if (_clientIpPorts.TryAdd(e.IpPort, node))
            {
                _logger?.LogInformation("{client} is connected", e.IpPort);
            }
            else
            {
                _logger?.LogWarning("{client} is already exist! Updating info", e.IpPort);
                _clientIpPorts[e.IpPort].Dispose();
                _clientIpPorts[e.IpPort] = node;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error on client connected");
        }
    }

    public Task Stop()
    {
        startTimer?.Dispose();

        foreach (var node in _clientIpPorts.Values)
        {
            node.OnTime -= Node_OnTime;
            node.Dispose();
        }


        if (_server is not null)
        {
            _logger?.LogInformation("Stop listening");

            if (_server.IsListening)
            {
                foreach (var item in _server.GetClients())
                {
                    _server.DisconnectClient(item);
                }

                _server.Stop();
            }

            _server.Events.ClientConnected -= Server_ClientConnected;
            _server.Events.ClientDisconnected -= Server_ClientDisconnected;
            _server.Events.DataReceived -= Server_DataReceived;
            _server.Dispose();
            _server = null;
            _logger?.LogInformation("Stopped");
        }

        _clientIpPorts.Clear();

        return Task.CompletedTask;
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Stop();

                BindedPoint = null;
                OnClientConnected = null;
                OnClientDisconnected = null;
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
