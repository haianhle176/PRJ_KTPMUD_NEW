# Ke hoach Nen tang IoT Dong

## 1. Tom tat

Xay dung nen tang IoT da nguoi dung gom:

- **DeviceApp:** mot Console App duy nhat, moi instance co serial rieng va khai bao capability dong bang manifest.
- **HiveMQ Cloud:** truyen telemetry, trang thai, lenh dieu khien va phan hoi lenh qua MQTT/TLS.
- **Backend:** ASP.NET Core Web API, MQTT Worker, Dapper, Azure SQL Database.
- **WPF App:** quan ly thiet bi, theo doi du lieu, dieu khien actuator va tao luat If-Then.
- **MVP:** thiet bi dang ky -> nguoi dung claim -> telemetry hien thi -> gui lenh -> nhan ACK -> automation tu kich hoat.

### Nguyen tac trien khai: tinh nang truoc, giao dien sau

Du an bat buoc di theo huong **backend-first, UI-last**. Trong cac phase P0-P5, nhom tap trung hoan thien va kiem thu toan bo tinh nang cot loi ma chua phu thuoc vao WPF:

- DeviceApp ket noi HiveMQ, gui manifest, presence va telemetry.
- Backend xu ly tai khoan, claim thiet bi, ownership va luu tru du lieu.
- API va MQTT Worker thuc hien dieu khien actuator, ACK va automation.
- Moi use case duoc kiem thu truoc bang DeviceApp, Swagger/Postman va automated tests.
- Chua dau tu theme, animation, dashboard phuc tap hoac bieu do trong giai doan nay.

Chi bat dau P6 - WPF App khi luong end-to-end `DeviceApp -> HiveMQ -> Worker -> SQL -> API` va luong command nguoc lai da hoat dong on dinh. WPF khi do chi dong vai tro trinh bay va goi cac API da duoc xac minh, khong chua business rule cot loi.

## 2. Kien truc va giao tiep

### Thanh phan

- `IoTPlatform.Domain`: entity, enum, business rule thuan.
- `IoTPlatform.Application`: use case, DTO, validation, interface repository/service.
- `IoTPlatform.Infrastructure`: Dapper, SQL Server, MQTT, JWT, hashing.
- `IoTPlatform.Api`: REST API, authentication, authorization.
- `IoTPlatform.Worker`: MQTT subscriber, telemetry processing, automation engine, command timeout.
- `IoTPlatform.DeviceApp`: mo phong thiet bi va capability.
- `IoTPlatform.Wpf`: MVVM, API client, dashboard.
- Cac project test tuong ung cho Domain, Application, API va Worker.

API va Worker tach process nhung dung chung Application, Infrastructure va Azure SQL.

### Capability dong

DeviceApp khong co cac loai `AirConditioner`, `Pump` dong cung. Khi chay, nguoi dung cau hinh manifest gom:

- `key`: ma capability on dinh trong thiet bi.
- `displayName`.
- `kind`: `sensor` hoac `actuator`.
- `dataType`: `number`, `boolean`, `string`.
- `unit`.
- `minValue`, `maxValue`.
- `isWritable`.
- `supportedCommands`, mac dinh `ON/OFF` cho actuator boolean.

Backend luu manifest theo tung thiet bi. Thay doi manifest phai tang `manifestVersion`; capability bi bo di duoc danh dau inactive thay vi xoa lich su.

### MQTT contract

Topic:

```text
iot/devices/{serial}/manifest
iot/devices/{serial}/telemetry
iot/devices/{serial}/state
iot/devices/{serial}/commands
iot/devices/{serial}/commands/ack
iot/devices/{serial}/presence
```

Quy uoc:

- TLS bat buoc; credential khong hard-code trong source.
- Telemetry dung QoS 1, khong retain.
- Manifest va trang thai hien tai dung QoS 1, retained.
- Command va ACK dung QoS 1.
- Presence dung retained message va MQTT Last Will.
- Moi message co `messageId`, `serial`, `timestampUtc`, `schemaVersion`.
- Command co `commandId`, `capabilityKey`, `desiredValue`.
- ACK co cung `commandId`, trang thai `accepted`, `completed` hoac `failed`.
- Worker xu ly idempotent theo `messageId` va `commandId`.

### REST API chinh

```text
POST   /api/auth/register
POST   /api/auth/login
POST   /api/auth/refresh
POST   /api/devices/claim
GET    /api/devices
GET    /api/devices/{id}
PATCH  /api/devices/{id}
DELETE /api/devices/{id}/claim
GET    /api/devices/{id}/telemetry
GET    /api/devices/{id}/latest-values
POST   /api/devices/{id}/commands
GET    /api/commands/{id}
GET    /api/automation-rules
POST   /api/automation-rules
PUT    /api/automation-rules/{id}
PATCH  /api/automation-rules/{id}/enabled
DELETE /api/automation-rules/{id}
GET    /api/automation-rules/{id}/executions
```

Moi truy van tai nguyen phai loc theo `UserId`; serial khong duoc dung de vuot qua ownership.

### Automation v1

Mot luat gom:

```text
IF source capability operator threshold
THEN target actuator = desired value
```

Ho tro toan tu `>`, `>=`, `<`, `<=`, `==`, `!=`.

Luat co `enabled`, `cooldownSeconds`, `lastTriggeredAt`, ten mo ta va hai thiet bi thuoc cung nguoi dung. Worker danh gia luat sau khi ghi telemetry; chi tao command khi dieu kien chuyen tu false sang true hoac cooldown da het. Moi lan danh gia/kich hoat/that bai duoc ghi lich su.

## 3. Mo hinh du lieu

Cac bang chinh:

- `Users`: tai khoan, password hash, trang thai.
- `RefreshTokens`: phien dang nhap va thu hoi token.
- `Devices`: serial, ten nguoi dung dat, owner, online status, last seen, manifest version.
- `PairingCodes`: code hash, serial, het han, da su dung.
- `Capabilities`: device, key, kind, data type, unit, gioi han, writable, active.
- `TelemetryRecords`: capability, gia tri typed/JSON, timestamp, message ID.
- `DeviceLatestValues`: mot gia tri moi nhat cho moi capability.
- `DeviceCommands`: actuator, desired value, trang thai, nguon manual/automation.
- `CommandAcknowledgements`: ACK va loi tu DeviceApp.
- `AutomationRules`: dieu kien, hanh dong, cooldown va trang thai.
- `AutomationExecutions`: ket qua danh gia va command duoc tao.
- `ProcessedMessages`: chong xu ly MQTT trung.
- `AuditLogs`: claim, doi ten, dieu khien va thay doi luat.

Rang buoc quan trong:

- `Devices.Serial` unique.
- `Capabilities(DeviceId, Key)` unique.
- `TelemetryRecords(CapabilityId, RecordedAtUtc)` co index.
- Pairing code het han sau 10 phut va chi dung mot lan.
- Du lieu telemetry mac dinh giu 30 ngay; latest value giu vo thoi han.

## 4. Cac giai doan trien khai

### Thu tu uu tien phat trien

| Nhom uu tien | Phase | Trong tam |
| --- | --- | --- |
| Tinh nang cot loi | P0-P2 | Contract, database, device identity, capability dong, tai khoan va claim |
| Xu ly nghiep vu | P3-P5 | Telemetry, command/ACK va automation engine |
| Giao dien | P6 | WPF tich hop cac API va use case da hoan chinh |
| Hoan thien | P7-P8 | Cloud, kiem thu, bao cao va tinh nang mo rong |

Khong chuyen sang P6 neu cac tieu chi nghiem thu cua P1-P5 chua dat. Neu thieu thoi gian, uu tien cat giam do hoa, animation, SignalR va dashboard nang cao; khong cat giam capability dong, telemetry, command/ACK, ownership hoac automation.

### P0 - Dac ta va nen mong

- Chot glossary, trang thai thiet bi/lenh, JSON schema va topic MQTT.
- Tao solution, project boundaries, configuration va logging.
- Tao Azure SQL schema bang migration scripts co version.
- Thiet lap secret bang user-secrets/local environment va Azure App Settings.
- Hoan thanh Architecture Diagram, ERD ban dau va API/MQTT contract.

**Hoan thanh khi:** solution build duoc, database khoi tao lap lai duoc va contract khong con diem mo ho.

### P1 - Device identity va capability discovery

- DeviceApp tao va luu serial on dinh tai may.
- Man hinh console cho chon capability, che do tu sinh hoac nhap tay.
- Ket noi HiveMQ Cloud, Last Will, reconnect va exponential backoff.
- Gui manifest/presence; Worker luu device chua claim va capability.
- Sinh pairing code an toan.

**Hoan thanh khi:** nhieu DeviceApp co manifest khac nhau cung online va xuat hien trong database.

### P2 - Tai khoan va claim thiet bi

- Dang ky, dang nhap JWT, refresh token va password hashing.
- Claim bang pairing code; doi ten va phan loai thiet bi.
- Ownership authorization tren toan bo API.
- Audit log cho dang nhap, claim, unclaim va doi ten.

**Hoan thanh khi:** hai tai khoan khong the xem hoac dieu khien thiet bi cua nhau.

### P3 - Telemetry pipeline

- DeviceApp sinh hoac nhan du lieu thu cong theo data type/range.
- Worker validate manifest, timestamp, kieu du lieu va deduplicate.
- Ghi lich su va upsert latest value.
- API danh sach, chi tiet, latest value va lich su co phan trang.
- Chinh sach online/offline dua tren presence va `LastSeenAt`.

**Hoan thanh khi:** du lieu di DeviceApp -> HiveMQ -> Worker -> SQL -> API on dinh.

### P4 - Dieu khien actuator

- API tao command sau khi kiem tra ownership va writable capability.
- Worker publish command; DeviceApp thuc thi va gui ACK.
- Theo doi `Pending -> Sent -> Accepted -> Completed/Failed/TimedOut`.
- Retry gioi han cho loi tam thoi; khong lap lai command da completed.
- Console hien thi desired state va reported state.

**Hoan thanh khi:** lenh ON/OFF tren API thay doi DeviceApp va phan hoi trang thai chinh xac.

### P5 - Automation engine

- CRUD luat mot dieu kien, enable/disable va validation tuong thich data type.
- Worker danh gia luat theo telemetry, edge trigger va cooldown.
- Tao command voi nguon `Automation`.
- Luu execution history va nguyen nhan bo qua/that bai.
- Ngan vong lap bang cach chi dung sensor telemetry lam nguon v1.

**Hoan thanh khi:** sensor cua thiet bi A tu dieu khien actuator cua thiet bi B, co bang chung lich su day du.

### P6 - WPF App

- Khong trien khai business rule moi tai WPF; moi thao tac nghiep vu phai di qua Web API.
- MVVM, DI, typed API client va secure token storage.
- Man hinh dang nhap/dang ky.
- Danh sach thiet bi online/offline, claim, doi ten va loc.
- Chi tiet thiet bi: capability, latest value, lich su va actuator control.
- Trinh tao luat If-Then bang dropdown dua tren capability thuc te.
- Trang thai loading, empty, validation, reconnect va loi API ro rang.
- Cap nhat gan realtime bang polling 3-5 giay; SignalR la P8.

**Hoan thanh khi:** toan bo MVP duoc demo chi bang WPF va DeviceApp.

### P7 - Cloud, kiem thu va bao cao

- Deploy API va Worker len Azure, SQL tren Azure SQL Database.
- Cau hinh HiveMQ credentials, CORS, HTTPS, health checks va structured logs.
- Seed tai khoan/demo scenario khong chua secret.
- Integration test voi broker test hoac MQTT abstraction.
- Script demo chuan: claim -> telemetry -> manual command -> automation.
- Hoan thien diagram, test report, deployment guide va danh gia gioi han.

### P8 - Mo rong sau MVP

- SignalR realtime.
- Luat nhieu dieu kien AND/OR va lich thoi gian.
- Notification khi offline hoac vuot nguong.
- Device grouping theo phong/khu vuc/tag.
- Dashboard tong hop va xuat CSV.
- Role admin, chia se thiet bi, rate limiting.
- Telemetry aggregation va retention job.
- Device credential rieng, certificate-based authentication va OTA config.

## 5. Noi dung diagram cho bao cao

### System Architecture Diagram

Node va luong:

```text
DeviceApp instances
    <-> MQTT/TLS
HiveMQ Cloud
    <-> subscribe/publish
ASP.NET Core MQTT Worker
    <-> Dapper
Azure SQL Database
    <-> Dapper
ASP.NET Core Web API
    <-> HTTPS/JSON + JWT
WPF Application
```

The hien rieng telemetry upstream, command downstream, ACK, authentication va Worker/API dung chung database.

### Use Case Diagram

Actors:

- `User`
- `DeviceApp`
- `HiveMQ Cloud`
- `System Worker`

Use cases chinh:

- Dang ky/dang nhap.
- Claim va doi ten thiet bi.
- Xem online status/capability/telemetry.
- Gui lenh actuator.
- Tao, sua, bat/tat va xem lich su automation.
- Device dang ky manifest.
- Device publish telemetry/presence.
- Device nhan lenh va gui ACK.
- Worker luu du lieu, danh gia luat va phat lenh.

### Class Diagram

Nhom entity:

```text
User 1--* Device
Device 1--* Capability
Capability 1--* TelemetryRecord
Capability 1--* DeviceCommand
DeviceCommand 1--* CommandAcknowledgement
User 1--* AutomationRule
AutomationRule *--1 SourceCapability
AutomationRule *--1 TargetCapability
AutomationRule 1--* AutomationExecution
Device 1--* PairingCode
```

Bo sung service/interface:

- `IDeviceRepository`
- `ITelemetryRepository`
- `ICommandService`
- `IMqttPublisher`
- `IAutomationEvaluator`
- `IPairingService`
- `IUserContext`

### ERD

ERD trinh bay day du PK/FK, unique constraint, nullable field va cardinality cua cac bang trong muc mo hinh du lieu. Tach `TelemetryRecords` khoi `DeviceLatestValues` de giai thich toi uu doc dashboard ma khong mat lich su.

### Sequence Diagram bat buoc

Chuan bi bon sequence diagram:

1. Device khoi dong, ket noi, gui manifest va nhan pairing code.
2. User claim thiet bi qua WPF.
3. User gui command va DeviceApp tra ACK.
4. Telemetry kich hoat automation va command duoc gui sang thiet bi dich.

### State Diagram

- Device: `Discovered -> Unclaimed -> Claimed -> Online/Offline -> Revoked`.
- Command: `Pending -> Sent -> Accepted -> Completed`, voi nhanh `Failed` va `TimedOut`.

## 6. Kiem thu va tieu chi nghiem thu

- Unit test manifest validation, comparison operator, cooldown va state transition.
- Integration test repository voi SQL Server container hoac test database.
- API test authentication, ownership, pagination va invalid capability.
- MQTT test reconnect, duplicate message, malformed payload, offline device va delayed ACK.
- End-to-end test voi it nhat ba DeviceApp co capability khac nhau.
- Load test muc tieu ban dau: 100 thiet bi mo phong, moi thiet bi gui mot message/5 giay.
- Khong mat quyen so huu, khong dieu khien cheo tai khoan va khong tao command trung.
- Demo van hoat dong sau khi Worker hoac DeviceApp mat ket noi roi khoi dong lai.

## 7. Gia dinh da chot

- Nhom co it nhat 5 nguoi va hon 10 tuan.
- HiveMQ Cloud la broker chinh; Azure khong thay the MQTT broker.
- He thong da nguoi dung nhung chua co chia se thiet bi hoac role admin trong MVP.
- DeviceApp dung manifest runtime, khong dung danh sach device type dong cung.
- Pairing code la co che claim mac dinh.
- Automation v1 chi co mot dieu kien va mot hanh dong.
- WPF dung polling trong MVP; realtime SignalR thuoc P8.
- Thoi gian luu va truyen deu dung UTC; WPF chuyen sang gio dia phuong.
- UI duoc trien khai sau khi hoan thanh API, MQTT pipeline, command va automation.
