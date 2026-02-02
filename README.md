# Psxbox.Gateway

**Psxbox.Gateway** - Gateway va IoT platformalar integratsiya kutubxonalari.

## Loyihalar

### 1. Psxbox.GatewayPoints
MQTT va TCP gateway pointlarni boshqarish kutubxonasi.
- TCP Gateway Point
- MQTT Gateway Point
- TCP to TCP Proxy
- Collector Node

### 2. Psxbox.TBGateway
ThingsBoard IoT platformasi bilan integratsiya.
- Gateway API mijozi
- Telemetriya yuborish
- RPC buyruqlar
- Attributes boshqaruvi

## O'rnatish

```bash
# Gateway Points
dotnet add reference path/to/Psxbox.Gateway/Psxbox.GatewayPoints/Psxbox.GatewayPoints.csproj

# ThingsBoard Gateway
dotnet add reference path/to/Psxbox.Gateway/Psxbox.TBGateway/Psxbox.TBGateway.csproj
```

## Bog'liqliklar

- `Psxbox.MQTTClient` - MQTT mijoz funksiyalari
- `Psxbox.Utils` - Yordamchi funksiyalar

## MyGateway Loyihasida Foydalanish

Ushbu kutubxonalar MyGateway IoT Gateway tizimida quyidagi vazifalarni bajaradi:

- ?? **Protocol Gateway** - Turli protokollar orasida ko'prik
- ?? **Cloud Integration** - ThingsBoard va boshqa platformalar
- ?? **Data Aggregation** - Ko'p qurilmalardan ma'lumot to'plash
- ?? **Remote Control** - Masofadan qurilmalarni boshqarish

## Litsenziya

MIT License
