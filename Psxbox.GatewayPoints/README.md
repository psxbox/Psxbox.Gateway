# Psxbox.GatewayPoints

**Psxbox.GatewayPoints** - MQTT va TCP gateway pointlarni boshqarish va mijozlar bilan aloqani ta'minlash uchun kutubxona.

## Xususiyatlari

- ?? **TCP Gateway Point** - TCP server sifatida mijozlarni qabul qilish
- ?? **MQTT Gateway Point** - MQTT orqali mijozlar bilan aloqa
- ?? **TCP to TCP Gateway** - TCP mijozlarni boshqa TCP server bilan bog'lash
- ?? **Mijozlar ro'yxati** - Ulanuvchi mijozlar nomini boshqarish
- ?? **Event-driven** - Mijozlar ulanganda/uzilganda eventlar
- ? **Async operatsiyalar** - To'liq asinxron ishlash

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.GatewayPoints/Psxbox.GatewayPoints.csproj
```

## Bog'liqliklar

- `SuperSimpleTcp` (v3.0.20) - TCP server/client funksiyalari
- `Microsoft.Extensions.Logging.Abstractions` (v10.0.0) - Logging
- `Microsoft.Extensions.Configuration.Abstractions` (v10.0.0) - Configuration
- `Psxbox.MQTTClient` - MQTT mijoz funksiyalari

## Foydalanish

### TCP Gateway Point - TCP Server

```csharp
using Psxbox.GatewayPoint;
using Microsoft.Extensions.Logging;

var logger = loggerFactory.CreateLogger<TcpGatewayPoint>();
var tcpGateway = new TcpGatewayPoint("0.0.0.0:5000", logger);

// Mijoz ulanganda event
tcpGateway.OnClientConnected += async (clientInfo) =>
{
    var (clientName, timestamp) = clientInfo;
    Console.WriteLine($"Mijoz ulandi: {clientName} at {timestamp}");
    return Task.CompletedTask;
};

// Mijoz uzilganda event
tcpGateway.OnClientDisconnected += async (clientInfo) =>
{
    var (clientName, timestamp) = clientInfo;
    Console.WriteLine($"Mijoz uzildi: {clientName} at {timestamp}");
    return Task.CompletedTask;
};

// Gateway ni boshlash
await tcpGateway.Start();

// Mijozga xabar yuborish
await tcpGateway.SendMessageToClient("192.168.1.100:52341", new byte[] { 0x01, 0x02, 0x03 });

// Gateway ni to'xtatish
await tcpGateway.Stop();
```

### MQTT Gateway Point

```csharp
var mqttGateway = new MqttGatewayPoint("mqtt://broker.hivemq.com:1883", logger);

// Faqat ma'lum mijozlarni qabul qilish
mqttGateway.UseClientNamesList = true;
mqttGateway.ClientNamesList = new List<string> 
{ 
    "device001", 
    "device002", 
    "sensor123" 
};

await mqttGateway.Start();

// MQTT orqali xabar yuborish
await mqttGateway.SendMessageToClient("device001", messageData);
```

### TCP to TCP Gateway - Proxy Mode

```csharp
// Backend server
var backendGateway = new TcpGatewayPoint("192.168.1.200:5001", logger);

// Frontend gateway (proxy)
var frontendGateway = new Tcp2TcpGatewayPoint("0.0.0.0:5000", logger);

// Ikki gateway ni bog'lash
frontendGateway.BindedPoint = backendGateway;

await backendGateway.Start();
await frontendGateway.Start();

// Endi frontend:5000 ga kelgan barcha xabarlar backend:5001 ga yo'naltiriladi
```

### Collector Node - Ma'lumot To'plash

```csharp
var collector = new CollectorNode(configuration, logger);

// Ma'lumotlarni to'plash
await collector.Start();

// Statistika
Console.WriteLine($"Ulangan mijozlar: {collector.ConnectedClientsCount}");
```

### Collector Modem Point - Modem Orqali Aloqa

```csharp
var modemPoint = new CollectorModemPoint("/dev/ttyUSB0,9600,8,N,1", logger);

// GSM modem orqali ma'lumot to'plash
await modemPoint.Start();
```

## IGatewayPoint Interfeysi

```csharp
public interface IGatewayPoint : IDisposable
{
    // Eventlar
    event Func<(string, DateTimeOffset), Task>? OnClientConnected;
    event Func<(string, DateTimeOffset), Task>? OnClientDisconnected;
    
    // Xususiyatlar
    IGatewayPoint? BindedPoint { get; set; }
    bool UseClientNamesList { get; set; }
    bool IsStarted { get; }
    IEnumerable<string> ClientNamesList { get; set; }
    
    // Metodlar
    Task SendMessageToClient(string clientName, byte[] data);
    Task Start();
    Task Stop();
}
```

## Arxitektura

```
IGatewayPoint (interface)
    ??? TcpGatewayPoint          ? TCP server
    ??? MqttGatewayPoint         ? MQTT mijoz
    ??? MqttGatewayPoint2        ? MQTT mijoz v2
    ??? Tcp2TcpGatewayPoint      ? TCP proxy
    ??? CollectorNode            ? Ma'lumot to'plag'ich
    ??? CollectorModemPoint      ? Modem orqali aloqa
```

## MyGateway Loyihasida Foydalanish

MyGateway tizimida ushbu kutubxona quyidagi vazifalarni bajaradi:

- ?? **PLC Gateway** - PLC qurilmalar bilan TCP orqali aloqa
- ?? **Data Collector** - Bir nechta qurilmalardan ma'lumot to'plash
- ?? **MQTT Bridge** - TCP va MQTT orasida ko'prik
- ?? **Remote Access** - Masofadan qurilmalarga kirish
- ?? **Protocol Converter** - Turli protokollar orasida konvertatsiya

### Configuration Example

```json
{
  "Gateway": {
    "TcpPoint": {
      "Address": "0.0.0.0:5000",
      "UseClientList": true,
      "AllowedClients": ["192.168.1.100", "192.168.1.101"]
    },
    "MqttPoint": {
      "Broker": "mqtt://localhost:1883",
      "ClientId": "gateway-001",
      "Topics": ["devices/+/telemetry"]
    }
  }
}
```

## Xavfsizlik

- IP-address filtrlash (ClientNamesList)
- Avtomatik reconnect mexanizmi
- Connection timeout boshqaruvi
- Error handling va logging

## Litsenziya

MIT License
