# Project Context

## 1. Muc tieu du an

Xay dung mot nen tang IoT dong, da nguoi dung, cho phep ket noi cac DeviceApp mo phong ma khong dong cung loai thiet bi. Moi thiet bi tu khai bao cac capability khi khoi dong; nguoi dung claim, dat ten, theo doi, dieu khien va ghep cac thiet bi bang luat If-Then.

MVP phai chung minh duoc luong end-to-end:

```text
DeviceApp -> HiveMQ Cloud -> MQTT Worker -> Azure SQL -> Web API -> WPF
DeviceApp <- HiveMQ Cloud <- MQTT Worker <- Web API <- WPF
```

Tai lieu ke hoach day du: [docs/IoT-Platform-Implementation-Plan.md](docs/IoT-Platform-Implementation-Plan.md).

## 2. Nguyen tac phat trien

- **Tinh nang truoc, giao dien sau:** hoan thanh P0-P5 truoc khi bat dau WPF tai P6.
- **Backend-first:** business rule nam trong Domain/Application/Worker, khong nam trong WPF.
- **Contract-first:** chot MQTT payload, REST DTO, state machine va database schema truoc khi trien khai.
- **Vertical validation:** moi use case duoc kiem thu bang DeviceApp, Swagger/Postman va automated tests truoc UI.
- **Dynamic capability:** backend khong su dung danh sach loai thiet bi dong cung.
- **Multi-tenant security:** moi tai nguyen phai duoc authorization theo `UserId`.
- **Idempotency:** MQTT QoS 1 khong duoc lam trung telemetry, command hay automation execution.
- **UTC everywhere:** backend va database luu UTC; client chuyen sang gio dia phuong.

Khong chuyen sang P6 neu telemetry, command/ACK va automation chua hoat dong end-to-end. Neu thieu thoi gian, cat giam theme, animation, dashboard nang cao va SignalR truoc khi cat tinh nang cot loi.

## 3. Kien truc muc tieu

### Technology stack

- C# va .NET cho tat ca application.
- ASP.NET Core Web API cho REST API.
- .NET Worker Service cho MQTT ingestion, command publishing va automation.
- Dapper cho data access.
- Azure SQL Database/SQL Server cho persistence.
- HiveMQ Cloud cho MQTT broker qua TLS.
- WPF theo MVVM cho desktop client.
- JWT access token va rotating refresh token cho authentication.

### Project boundaries

- `IoTPlatform.Domain`: entity, value object, enum va business rule thuan.
- `IoTPlatform.Application`: use case, DTO, validator va abstraction.
- `IoTPlatform.Infrastructure`: Dapper, SQL, MQTT, JWT va hashing implementation.
- `IoTPlatform.Api`: HTTP endpoint, authentication va authorization.
- `IoTPlatform.Worker`: MQTT consumer, automation evaluator va command dispatcher.
- `IoTPlatform.DeviceApp`: console simulator voi serial va runtime manifest.
- `IoTPlatform.Wpf`: presentation, MVVM va typed API client.
- Test projects bao phu Domain, Application, API, Infrastructure va Worker.

API va Worker la hai process rieng, dung chung Application, Infrastructure va database.

### Deployment topology da chot

```text
WPF (local) -> Web API (Azure App Service F1) -> Azure SQL
DeviceApp (local) <-> HiveMQ Cloud <-> Worker (local laptop) -> Azure SQL
```

- `IoTPlatform.Api` duoc publish len mot Azure App Service goi Free F1.
- `IoTPlatform.Worker` chay local tren laptop, khong deploy thanh WebJob hay App Service trong pham vi hien tai.
- `IoTPlatform.DeviceApp` va WPF chay local.
- HiveMQ broker va SQL Database chay tren cloud.
- Worker local va API Azure dung chung Azure SQL Database.
- F1 khong co Always On; API co the ngu khi khong co HTTP traffic va can request dau tien de khoi dong lai.
- MQTT processing chi hoat dong khi laptop chay Worker dang bat va co ket noi Internet.

## 4. Domain va contract cot loi

### Dynamic capability manifest

Moi capability co:

- `key`, `displayName`, `kind`, `dataType`, `unit`.
- `minValue`, `maxValue`, `isWritable`, `supportedCommands`.
- `kind`: `sensor` hoac `actuator`.
- `dataType`: `number`, `boolean` hoac `string`.

Manifest co `manifestVersion`. Capability bi loai bo phai chuyen sang inactive de bao toan lich su.

### MQTT topics

```text
iot/devices/{serial}/manifest
iot/devices/{serial}/telemetry
iot/devices/{serial}/state
iot/devices/{serial}/commands
iot/devices/{serial}/commands/ack
iot/devices/{serial}/presence
```

- MQTT bat buoc TLS va QoS 1.
- Manifest, state va presence duoc retained; telemetry khong retained.
- Presence dung Last Will.
- Moi message co `messageId`, `serial`, `timestampUtc`, `schemaVersion`.
- Command co `commandId`, `capabilityKey`, `desiredValue`.
- ACK co `commandId` va ket qua `accepted`, `completed` hoac `failed`.

### Automation v1

```text
IF source capability operator threshold
THEN target actuator = desired value
```

- Toan tu: `>`, `>=`, `<`, `<=`, `==`, `!=`.
- Mot rule co mot condition va mot action.
- Source v1 chi la sensor; target phai la writable actuator.
- Hai thiet bi phai thuoc cung user.
- Rule dung edge trigger va `cooldownSeconds` de han che lap lenh.

### Command state machine

```text
Pending -> Sent -> Accepted -> Completed
                    |             |
                    +-> Failed    +-> Failed
Pending/Sent/Accepted -> TimedOut
```

## 5. Du lieu chinh

Cac aggregate/table du kien:

- `Users`, `RefreshTokens`.
- `Devices`, `PairingCodes`, `Capabilities`.
- `TelemetryRecords`, `DeviceLatestValues`.
- `DeviceCommands`, `CommandAcknowledgements`.
- `AutomationRules`, `AutomationExecutions`.
- `ProcessedMessages`, `AuditLogs`.

Rang buoc quan trong:

- `Devices.Serial` unique.
- `Capabilities(DeviceId, Key)` unique.
- Pairing code duoc hash, het han sau 10 phut va chi dung mot lan.
- Telemetry mac dinh giu 30 ngay; latest value giu vo thoi han.
- Moi repository query du lieu cua user phai co ownership filter.

## 6. Quy uoc bao mat va van hanh

- Khong commit connection string, HiveMQ credential, JWT secret hay pairing code ro.
- Worker/DeviceApp local dung environment variables hoac .NET user-secrets; Web API tren Azure dung App Settings/Connection Strings.
- Password dung password hasher chuan, khong tu viet thuat toan ma hoa.
- Log co cau truc va correlation ID, nhung khong ghi password, token hay secret.
- Worker phai reconnect MQTT bang exponential backoff.
- API cung cap health check cho database; Worker local log trang thai ket noi database va HiveMQ.

## 7. Definition of Done chung

Mot task chi hoan thanh khi:

- Code build thanh cong va khong dua secret vao repository.
- Co test phu hop voi muc do rui ro cua thay doi.
- Validation, error path, logging va ownership da duoc xu ly.
- Public contract va tai lieu lien quan da duoc cap nhat.
- Use case co the chung minh bang lenh, test hoac demo lap lai duoc.
