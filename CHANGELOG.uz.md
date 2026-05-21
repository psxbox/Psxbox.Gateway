# O'zgarishlar Tarixi

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
