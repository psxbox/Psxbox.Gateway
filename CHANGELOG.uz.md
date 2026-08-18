# O'zgarishlar Tarixi

## [2.0.0] - 2026-08-18

### 💥 Breaking

- Uchta event imzosi qiymat qaytaradigan bo'ldi — handler qaytargan satr RPC javobidagi `message` ga tushadi, `null` esa default xabarni qoldiradi:
  - `OnDeleteDevice`: `Func<string, Task>` → `Func<string, Task<string?>>`
  - `OnRenameDevice`: `Func<string, string, Task>` → `Func<string, string, Task<string?>>`
  - `OnSetDeviceInfo`: `Func<string, object, Task>` → `Func<string, object, Task<string?>>`
- Gateway-qurilma yo'lida (`v1/gateway/rpc`) `READ`/`WRITE` javobidagi `data.data` bo'sh obyekt (`{}`) o'rniga handler natijasini qaytaradi — natija yo'q bo'lsa `null`.
- `GATEWAY_PING`, `STATUS_GATEWAY`, `GATEWAY_STATS`, `GATEWAY_VERSION` javobni endi bitta marta va `EnqueueAsync` orqali yozadi. Avval javob topikiga ikkita xabar ketardi (xom shakl `PublishAsync` bilan, so'ng qo'shimcha `{success, message}`).

### 🔄 O'zgartirildi

- Ikkita RPC dispatcher (`RpcHandlerAsync` — self-device, `GatewayRpcHandlerAsync` — gateway-device) bitta oqimga birlashtirildi: `RpcRequest` → `ExecuteRpcAsync` → `BuildRpcResponse`. Natijada **barcha metodlar ikkala transportda ham ochiq**:
  - `NEW_DEVICE`, `DELETE_DEVICE`, `RENAME_DEVICE`, `ENABLE_DEVICE` — avval faqat self-device yo'lida edi
  - `READ_DATA`/`WRITE_DATA` aliaslari — avval faqat gateway yo'lida edi
- Self-device javobiga `data` maydoni qo'shildi; muvaffaqiyat va xato javoblari endi bir xil maydonlarga ega.
- Self-device yo'lida `READ_DEVICE`/`WRITE_DEVICE` natijasi endi tashlanmaydi, javobning `data` maydoniga tushadi.
- Foydali yuk ikkala transportda `params.settings ?? params` orqali olinadi (`RpcRequest.Payload`) — self yo'lidagi `settings` o'rami gateway yo'liga yuborilsa ham to'g'ri ochiladi. Avval self yo'lida `settings` majburiy edi va yo'q bo'lsa `NullReferenceException` otilardi.
- Qurilma nomi ikkala transportda `RpcRequest.DeviceName` orqali normallashtiriladi (gateway yo'lida konvertdagi `device`, self yo'lida `params.deviceName` yoki `params.name`).

### ✨ Qo'shildi

- `RpcRequest` / `RpcTransport` — ikkala oqimning umumiy ko'rinishi.
- `RpcResult` — metod natijasi; `RpcResult.Raw` gateway-boshqaruv metodlari uchun TB widget'i kutadigan xom shaklni saqlaydi.

---


## [1.1.0] - 2026-07-27

### ✨ Qo'shildi

- `GatewayClient` testable qilindi: `IMqttReconnectClient` injection orqali

### 🐛 Tuzatildi

- `ConnectDeviceAsync`/`DisconnectDeviceAsync` enqueue fallback orqali yo'naltirildi
- MQTT publish xatoligida `attributeResponses` dan orphaned TCS olib tashlandi
- `WaitForAttributeResponse` dagi TCS race condition tuzatildi

### 🚀 Optimallashtirildi

- Busy-wait polling o'rniga TCS ishlatildi, `JsonSerializerOptions` ko'tarildi, redundant lock olib tashlandi

### 🔄 O'zgartirildi

- Paket referenslari eng so'nggi versiyalarga yangilandi

---

## [1.0.1] - 2026-05-21

### 🔄 O'zgartirildi

- `Psxbox.GatewayPoints` va `Psxbox.TBGateway` loyihalarida `Microsoft.Extensions.Configuration` hamda `Microsoft.Extensions.Logging` paketlari `10.0.8` versiyasiga yangilandi
- Repositoryda kesh fayllarni boshqarish uchun `.gitignore` ga `*.lscache` qoidasi qo'shildi
- `GatewayClient` ichida `READ_DEVICE` va `WRITE_DEVICE` buyruqlari qo'llab-quvvatlandi

---

## [1.0.0] - 2026-04-22

### ✨ Qo'shildi

- `GatewayInfo` sinfiga `MaxConcurrentPublish` xususiyati qo'shildi — parallel nashrlar sonini cheklash uchun
- Ulangan qurilmalarni boshqarish va `SemaphoreSlim` yordamida nashr sonini cheklash mexanizmi qo'shildi
- `SendWithFallbackAsync` metodi qo'shildi — telemetriya va atributlarni xato va xabar navbati bilan yuborish uchun
- `Dispose` metodida resurslarni bo'shatish mexanizmi qo'shildi (`MqttGatewayPoint`)
- `MqttAutoReconnectClient` ga o'tish: `MqttGatewayPoint` va `MqttGatewayPoint2` sinflari yangilandi

### 🔄 O'zgartirildi

- `EnqueueAsync` metodida: navbatda kutayotgan xabarlar bo'lganda yangi xabar qo'shilmasligi ta'minlandi
- `OnConnected` metodida: xabarlarga obuna bo'lishdan oldin kechikish qo'shildi
- `MqttClientInfo` initsializatsiyasi `Bind` o'rniga `GetSection` orqali bajariladi (`MqttGatewayPoint`)
- `Microsoft.Extensions.Configuration` va `Microsoft.Extensions.Logging` paketlari `10.0.7` versiyasiga yangilandi

---
