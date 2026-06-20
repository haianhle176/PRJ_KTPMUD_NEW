# Architecture and sequence diagrams

## System architecture

```mermaid
flowchart LR
    subgraph Local["Máy người dùng / máy demo"]
        WPF["OmniConnect WPF\nDashboard & management"]
        DeviceApp["DeviceApp instances\nDynamic device simulator"]
        Worker["MQTT Worker\nIngestion, dispatch, automation"]
    end

    subgraph Cloud["Cloud services"]
        API["ASP.NET Core Web API\nAzure App Service"]
        Hub["SignalR DashboardHub"]
        MQTT["HiveMQ Cloud\nMQTT/TLS broker"]
        SQL[("Azure SQL Database")]
    end

    WPF -->|"HTTPS/JSON + JWT"| API
    API -->|"Dapper"| SQL
    API --- Hub
    Hub -->|"DashboardChanged"| WPF

    DeviceApp <-->|"MQTT/TLS QoS 1\nmanifest, presence, telemetry, command ACK"| MQTT
    Worker <-->|"MQTT/TLS QoS 1\nsubscribe upstream, publish commands"| MQTT
    Worker -->|"Dapper transactions"| SQL
    SQL -->|"RealtimeOutbox"| API

    DeviceApp -->|"HTTPS/JWT\nprovision device"| API
```

Luồng dữ liệu chính:

- Upstream: DeviceApp → HiveMQ → Worker → Azure SQL.
- Downstream: WPF/API hoặc automation → Azure SQL → Worker → HiveMQ → DeviceApp.
- Realtime UI: SQL trigger → `RealtimeOutbox` → API dispatcher → SignalR → WPF.

## Sequence 1 — Device startup, manifest and pairing code

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant DA as DeviceApp
    participant API as Web API
    participant MQ as HiveMQ Cloud
    participant WK as MQTT Worker
    participant DB as Azure SQL

    User->>DA: Chọn loại và Add device
    DA->>API: POST /devices/provision
    API->>DB: Tạo device/capability cơ sở
    DB-->>API: Device provisioned
    API-->>DA: Provision success
    DA->>DA: Sinh serial/session/pairing code
    User->>DA: Start
    DA->>MQ: CONNECT TLS + Last Will
    DA->>MQ: PUBLISH retained manifest
    MQ-->>WK: manifest
    WK->>DB: Upsert device/capabilities
    WK->>DB: Lưu hash pairing code + expiry
    DA->>MQ: PUBLISH retained presence=online
    MQ-->>WK: presence=online
    WK->>DB: Cập nhật status/last seen
    DA-->>User: Hiển thị pairing code
```

## Sequence 2 — Claim device from OmniConnect

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant UI as OmniConnect WPF
    participant API as Web API
    participant CS as DeviceClaimService
    participant DB as Azure SQL
    participant RT as SignalR

    User->>UI: Nhập pairing code và tên thiết bị
    UI->>API: POST /devices/claim + JWT
    API->>CS: ClaimAsync(userId, request)
    CS->>DB: Bắt đầu transaction
    DB->>DB: Lock pairing code hợp lệ, chưa dùng, chưa hết hạn
    DB->>DB: Gán OwnerUserId và đánh dấu code đã dùng
    DB->>DB: Ghi AuditLog
    DB-->>CS: Commit + device
    CS-->>API: DeviceSummary
    API-->>UI: 200 OK
    DB-->>RT: RealtimeOutbox device/availableDevices
    RT-->>UI: DashboardChanged
    UI->>API: Refresh devices/capabilities
    API-->>UI: Thiết bị đã claim
```

## Sequence 3 — Manual actuator command and ACK

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant UI as OmniConnect WPF
    participant API as Web API
    participant DB as Azure SQL
    participant WK as MQTT Worker
    participant MQ as HiveMQ Cloud
    participant DA as DeviceApp
    participant RT as SignalR

    User->>UI: Bật/tắt actuator
    UI->>API: POST /devices/{id}/commands
    API->>DB: Validate ownership/capability và INSERT pending
    DB-->>RT: command event
    API-->>UI: Command pending
    WK->>DB: Poll pending commands
    WK->>MQ: PUBLISH device command
    WK->>DB: Status=sent
    MQ-->>DA: command
    DA->>DA: Validate và áp dụng desired value
    DA->>MQ: ACK accepted
    MQ-->>WK: ACK accepted
    WK->>DB: Status=accepted
    DA->>MQ: ACK completed + reported value
    MQ-->>WK: ACK completed
    WK->>DB: Status=completed
    WK->>DB: Ghi state transition + upsert latest value
    DB-->>RT: command + telemetry events
    RT-->>UI: DashboardChanged
    UI->>API: Refresh latest/history
    API-->>UI: Trạng thái actuator mới
```

## Sequence 4 — Sensor telemetry triggers automation

```mermaid
sequenceDiagram
    autonumber
    participant Source as Source DeviceApp
    participant MQ as HiveMQ Cloud
    participant WK as MQTT Worker
    participant DB as Azure SQL
    participant Target as Target DeviceApp
    participant RT as SignalR
    participant UI as OmniConnect WPF

    Source->>MQ: PUBLISH sensor telemetry
    MQ-->>WK: telemetry
    WK->>DB: Begin ingestion transaction
    DB->>DB: Dedupe MessageId
    DB->>DB: Insert TelemetryRecords + upsert latest
    DB->>DB: Load enabled rules for source capability
    DB->>DB: Evaluate comparator, edge, schedule, cooldown
    alt Rule triggered
        DB->>DB: INSERT automation DeviceCommand
        DB->>DB: INSERT AutomationExecution(triggered)
        DB->>DB: UPDATE LastConditionResult/LastTriggeredAtUtc
        DB->>DB: COMMIT
        WK->>DB: Poll pending automation command
        WK->>MQ: PUBLISH target command
        MQ-->>Target: command
        Target->>Target: Apply desired value
        Target->>MQ: ACK completed + reported value
        MQ-->>WK: ACK completed
        WK->>DB: Update command + actuator telemetry/latest
        DB-->>RT: telemetry/command/automation events
        RT-->>UI: DashboardChanged
    else Rule skipped
        DB->>DB: INSERT AutomationExecution(skipped + reason)
        DB->>DB: UPDATE LastConditionResult
        DB->>DB: COMMIT
    end
```

