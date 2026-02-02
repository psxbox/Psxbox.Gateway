# Psxbox.TBGateway

**Psxbox.TBGateway** - ThingsBoard IoT platformasi bilan integratsiya qilish uchun Gateway Client kutubxonasi. ThingsBoard Gateway API orqali qurilmalarni ulash, telemetriya yuborish va RPC buyruqlarini qabul qilish imkonini beradi.

## Xususiyatlari

- ?? **ThingsBoard Gateway API** - Gateway qurilma sifatida ishlash
- ?? **Telemetriya yuborish** - Qurilmalardan ma'lumotlarni platformaga yuborish
- ?? **Attributes** - Qurilma atributlarini boshqarish
- ?? **RPC (Remote Procedure Call)** - Masofadan buyruqlarni qabul qilish
- ?? **Ko'p qurilmalar** - Bir gateway orqali bir nechta qurilmalarni ulash
- ? **Async/Await** - To'liq asinxron operatsiyalar
- ?? **Real-time** - Real vaqt rejimida ma'lumot almashinuvi

## O'rnatish

```bash
dotnet add reference Shared/Psxbox.TBGateway/Psxbox.TBGateway.csproj
```

## Bog'liqliklar

- `Microsoft.Extensions.Configuration.Abstractions` (v10.0.0) - Configuration
- `Microsoft.Extensions.Configuration.Binder` (v10.0.0) - Configuration binding
- `Microsoft.Extensions.Logging.Abstractions` (v10.0.0) - Logging
- `Psxbox.MQTTClient` - MQTT mijoz
- `Psxbox.Utils` - Yordamchi funksiyalar

## Foydalanish

### Gateway Mijozni Yaratish

```csharp
using Psxbox.TBGateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Configuration dan yaratish
var gatewayClient = GatewayClient.GetGatewayClient(configuration, logger);

// Yoki to'g'ridan-to'g'ri
var gatewayInfo = new GatewayInfo
{
    Host = "demo.thingsboard.io",
    Port = 1883,
    Token = "YOUR_GATEWAY_ACCESS_TOKEN"
};

var gatewayClient = new GatewayClient(gatewayInfo, logger);
```

### Gateway ni Ishga Tushirish

```csharp
// Gateway ni boshlash
await gatewayClient.StartAsync();

// Ulanish holatini tekshirish
if (gatewayClient.IsConnected)
{
    Console.WriteLine("Gateway ThingsBoard ga ulandi!");
}
```

### Qurilmani Gateway ga Ulash

```csharp
// Qurilmani ulash (connect)
await gatewayClient.Connect("Device001");
await gatewayClient.Connect("PLC-100");
await gatewayClient.Connect("Sensor-Temperature-01");

Console.WriteLine("Qurilmalar gateway ga ulandi");
```

### Telemetriya Yuborish

```csharp
// Telemetriya ob'ektini yaratish
var telemetry = new
{
    temperature = 25.5,
    humidity = 60.3,
    pressure = 1013.25
};

// Bitta qurilma uchun telemetriya
await gatewayClient.PublishTelemetry("Sensor-001", telemetry);

// Bir nechta qurilmalar uchun
var telemetryData = new Dictionary<string, object>
{
    ["Device001"] = new { voltage = 220.5, current = 5.2 },
    ["Device002"] = new { temperature = 30.1, status = "OK" }
};

await gatewayClient.PublishTelemetry(telemetryData);
```

### Telemetriya bilan Timestamp

```csharp
// Timestamp bilan telemetriya
var telemetryWithTs = new
{
    ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    values = new
    {
        temperature = 25.5,
        humidity = 60.3
    }
};

await gatewayClient.PublishTelemetry("Device001", telemetryWithTs);
```

### Attributes Yuborish

```csharp
// Client-side attributes
var attributes = new
{
    firmwareVersion = "1.2.3",
    serialNumber = "SN123456789",
    model = "PLC-1000"
};

await gatewayClient.PublishAttributes("Device001", attributes);

// Server-side attributes so'rash
var attributeKeys = new[] { "sharedKey1", "sharedKey2" };
var serverAttributes = await gatewayClient.RequestAttributes(
    "Device001", 
    attributeKeys
);

Console.WriteLine($"Attribute: {serverAttributes}");
```

### RPC Buyruqlarini Qabul Qilish

```csharp
// RPC xabarlarni qabul qilish
gatewayClient.OnRpc += async (deviceName, rpcMessage) =>
{
    Console.WriteLine($"RPC qabul qilindi: Device={deviceName}, Method={rpcMessage.Method}");
    
    // RPC metodini qayta ishlash
    switch (rpcMessage.Method)
    {
        case "setValue":
            var value = rpcMessage.Params["value"];
            Console.WriteLine($"Set value to: {value}");
            
            // Response yuborish
            await gatewayClient.PublishRpcResponse(
                deviceName, 
                rpcMessage.Id, 
                new { success = true }
            );
            break;
            
        case "getStatus":
            var status = new { online = true, temperature = 25.5 };
            await gatewayClient.PublishRpcResponse(
                deviceName, 
                rpcMessage.Id, 
                status
            );
            break;
            
        default:
            await gatewayClient.PublishRpcResponse(
                deviceName, 
                rpcMessage.Id, 
                new { error = "Unknown method" }
            );
            break;
    }
};
```

### Qurilmani Uzish

```csharp
// Qurilmani gateway dan uzish (disconnect)
await gatewayClient.Disconnect("Device001");

Console.WriteLine("Qurilma uzildi");
```

### Gateway ni To'xtatish

```csharp
// Gateway ni to'xtatish
await gatewayClient.StopAsync();
gatewayClient.Dispose();
```

## Configuration Sozlamalari

### appsettings.json

```json
{
  "Gateway": {
    "Host": "demo.thingsboard.io",
    "Port": 1883,
    "Token": "YOUR_GATEWAY_ACCESS_TOKEN"
  }
}
```

### Environment Variables

```bash
GATEWAY__HOST=demo.thingsboard.io
GATEWAY__PORT=1883
GATEWAY__TOKEN=your_gateway_access_token
```

## MQTT Topic'lar

Gateway quyidagi ThingsBoard topic'laridan foydalanadi:

| Topic | Maqsad |
|-------|--------|
| `v1/gateway/connect` | Qurilmani ulash |
| `v1/gateway/disconnect` | Qurilmani uzish |
| `v1/gateway/telemetry` | Telemetriya yuborish |
| `v1/gateway/attributes` | Attributes yuborish |
| `v1/gateway/attributes/request` | Attributes so'rash |
| `v1/gateway/attributes/response` | Attributes javobi |
| `v1/gateway/rpc` | RPC buyruqlarni qabul qilish |

## MyGateway Loyihasida Foydalanish

MyGateway tizimida TBGateway kutubxonasi quyidagi vazifalarni bajaradi:

- ?? **Cloud Integration** - ThingsBoard platformasi bilan integratsiya
- ?? **Data Aggregation** - Bir nechta qurilmalardan ma'lumot to'plash
- ?? **Real-time Monitoring** - Real vaqt rejimida monitoring
- ?? **Remote Control** - Masofadan qurilmalarni boshqarish
- ?? **Dashboard** - ThingsBoard dashboard orqali vizualizatsiya

### Worker Service Example

```csharp
public class ThingsBoardWorker : BackgroundService
{
    private readonly GatewayClient _gatewayClient;
    private readonly ILogger<ThingsBoardWorker> _logger;
    
    public ThingsBoardWorker(IConfiguration config, ILogger<ThingsBoardWorker> logger)
    {
        _gatewayClient = GatewayClient.GetGatewayClient(config, logger);
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _gatewayClient.StartAsync();
        
        // RPC handler
        _gatewayClient.OnRpc += HandleRpcAsync;
        
        // Qurilmalarni ulash
        await _gatewayClient.Connect("PLC-001");
        await _gatewayClient.Connect("Sensor-001");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            // Telemetriya to'plash va yuborish
            var telemetry = await CollectTelemetryAsync();
            await _gatewayClient.PublishTelemetry("PLC-001", telemetry);
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        
        await _gatewayClient.StopAsync();
    }
    
    private async Task HandleRpcAsync(string deviceName, RpcMessage rpcMessage)
    {
        _logger.LogInformation($"RPC: {deviceName} - {rpcMessage.Method}");
        
        // RPC ni qayta ishlash
        var response = new { success = true, result = "OK" };
        await _gatewayClient.PublishRpcResponse(deviceName, rpcMessage.Id, response);
    }
    
    private async Task<object> CollectTelemetryAsync()
    {
        // Ma'lumot to'plash
        return new
        {
            temperature = 25.5,
            pressure = 1013.25,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}
```

## Telemetriya Formatlari

### Oddiy Format

```json
{
  "temperature": 25.5,
  "humidity": 60.3
}
```

### Timestamp bilan

```json
{
  "ts": 1642684800000,
  "values": {
    "temperature": 25.5,
    "humidity": 60.3
  }
}
```

### Bir nechta qurilmalar

```json
[
  {
    "deviceName": "Device001",
    "telemetry": {
      "temperature": 25.5
    }
  },
  {
    "deviceName": "Device002",
    "telemetry": {
      "pressure": 1013.25
    }
  }
]
```

## RPC Format

### Request

```json
{
  "device": "Device001",
  "data": {
    "id": 1,
    "method": "setValue",
    "params": {
      "value": 100
    }
  }
}
```

### Response

```json
{
  "success": true,
  "result": "OK"
}
```

## Xatolarni Boshqarish

```csharp
try
{
    await gatewayClient.StartAsync();
    await gatewayClient.PublishTelemetry("Device001", telemetry);
}
catch (Exception ex)
{
    logger.LogError($"Gateway xatosi: {ex.Message}");
}
```

## Litsenziya

MIT License

## Qo'shimcha Ma'lumot

- ThingsBoard dokumentatsiya: https://thingsboard.io/docs/
- Gateway API: https://thingsboard.io/docs/reference/gateway-mqtt-api/
