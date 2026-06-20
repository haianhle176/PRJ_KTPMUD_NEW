# Domain and data model diagrams

## Entity relationship diagram

```mermaid
erDiagram
    USERS {
        uniqueidentifier Id PK
        nvarchar Email
        nvarchar NormalizedEmail UK
        nvarchar PasswordHash
        nvarchar DisplayName
        bit IsActive
    }

    REFRESH_TOKENS {
        uniqueidentifier Id PK
        uniqueidentifier UserId FK
        varbinary TokenHash UK
        datetime2 ExpiresAtUtc
        datetime2 RevokedAtUtc
    }

    DEVICES {
        uniqueidentifier Id PK
        uniqueidentifier OwnerUserId FK
        nvarchar Serial UK
        nvarchar DisplayName
        varchar Status
        nvarchar DeviceType
        int ManifestVersion
        datetime2 LastSeenAtUtc
    }

    PAIRING_CODES {
        uniqueidentifier Id PK
        uniqueidentifier DeviceId FK
        varbinary CodeHash UK
        datetime2 ExpiresAtUtc
        datetime2 UsedAtUtc
    }

    CAPABILITIES {
        uniqueidentifier Id PK
        uniqueidentifier DeviceId FK
        nvarchar Key
        nvarchar DisplayName
        varchar Kind
        varchar DataType
        bit IsWritable
        bit IsActive
    }

    TELEMETRY_RECORDS {
        bigint Id PK
        uniqueidentifier MessageId UK
        uniqueidentifier DeviceId FK
        uniqueidentifier CapabilityId FK
        varchar ValueType
        decimal NumberValue
        bit BooleanValue
        nvarchar StringValue
        datetime2 RecordedAtUtc
    }

    DEVICE_LATEST_VALUES {
        uniqueidentifier CapabilityId PK,FK
        uniqueidentifier DeviceId FK
        uniqueidentifier MessageId
        varchar ValueType
        decimal NumberValue
        bit BooleanValue
        nvarchar StringValue
        datetime2 RecordedAtUtc
    }

    DEVICE_COMMANDS {
        uniqueidentifier Id PK
        uniqueidentifier DeviceId FK
        uniqueidentifier CapabilityId FK
        uniqueidentifier OwnerUserId FK
        uniqueidentifier AutomationRuleId FK
        nvarchar CommandName
        varchar DesiredValueType
        varchar Source
        varchar Status
        datetime2 ExpiresAtUtc
    }

    COMMAND_ACKNOWLEDGEMENTS {
        bigint Id PK
        uniqueidentifier MessageId UK
        uniqueidentifier CommandId FK
        varchar Status
        varchar ReportedValueType
        bit ReportedBooleanValue
        nvarchar ErrorCode
    }

    AUTOMATION_RULES {
        uniqueidentifier Id PK
        uniqueidentifier OwnerUserId FK
        uniqueidentifier SourceCapabilityId FK
        uniqueidentifier TargetCapabilityId FK
        nvarchar Name
        nvarchar ComparisonOperator
        nvarchar CommandName
        int CooldownSeconds
        bit IsEnabled
        time ActiveFromTime
        time ActiveToTime
        int ActiveDaysOfWeek
    }

    AUTOMATION_EXECUTIONS {
        bigint Id PK
        uniqueidentifier AutomationRuleId FK
        uniqueidentifier TriggerMessageId
        uniqueidentifier CommandId FK
        varchar Outcome
        nvarchar Reason
        datetime2 CreatedAtUtc
    }

    NOTIFICATIONS {
        bigint Id PK
        uniqueidentifier OwnerUserId FK
        uniqueidentifier DeviceId FK
        uniqueidentifier CapabilityId FK
        varchar Type
        varchar Severity
        nvarchar Title
        bit IsRead
    }

    TELEMETRY_HOURLY_AGGREGATES {
        bigint Id PK
        uniqueidentifier DeviceId FK
        uniqueidentifier CapabilityId FK
        datetime2 BucketStartUtc
        decimal MinValue
        decimal MaxValue
        decimal AverageValue
        int SampleCount
    }

    AUDIT_LOGS {
        bigint Id PK
        uniqueidentifier UserId FK
        nvarchar Action
        nvarchar EntityType
        nvarchar EntityId
        datetime2 CreatedAtUtc
    }

    PROCESSED_MESSAGES {
        uniqueidentifier MessageId PK
        nvarchar Topic
        datetime2 ProcessedAtUtc
    }

    REALTIME_OUTBOX {
        bigint Id PK
        uniqueidentifier EventId UK
        uniqueidentifier OwnerUserId FK
        varchar Kind
        uniqueidentifier DeviceId
        nvarchar EntityId
        datetime2 DispatchedAtUtc
    }

    USERS ||--o{ REFRESH_TOKENS : owns
    USERS ||--o{ DEVICES : claims
    USERS ||--o{ DEVICE_COMMANDS : creates
    USERS ||--o{ AUTOMATION_RULES : owns
    USERS ||--o{ NOTIFICATIONS : receives
    USERS ||--o{ AUDIT_LOGS : performs
    USERS ||--o{ REALTIME_OUTBOX : receives

    DEVICES ||--o{ PAIRING_CODES : exposes
    DEVICES ||--|{ CAPABILITIES : declares
    DEVICES ||--o{ TELEMETRY_RECORDS : produces
    DEVICES ||--o{ DEVICE_LATEST_VALUES : has
    DEVICES ||--o{ DEVICE_COMMANDS : receives
    DEVICES ||--o{ NOTIFICATIONS : concerns
    DEVICES ||--o{ TELEMETRY_HOURLY_AGGREGATES : aggregates

    CAPABILITIES ||--o{ TELEMETRY_RECORDS : records
    CAPABILITIES ||--o| DEVICE_LATEST_VALUES : latest
    CAPABILITIES ||--o{ DEVICE_COMMANDS : targets
    CAPABILITIES ||--o{ AUTOMATION_RULES : source
    CAPABILITIES ||--o{ AUTOMATION_RULES : target
    CAPABILITIES ||--o{ NOTIFICATIONS : concerns
    CAPABILITIES ||--o{ TELEMETRY_HOURLY_AGGREGATES : aggregates

    DEVICE_COMMANDS ||--o{ COMMAND_ACKNOWLEDGEMENTS : acknowledges
    DEVICE_COMMANDS ||--o| AUTOMATION_EXECUTIONS : results_in
    AUTOMATION_RULES ||--o{ DEVICE_COMMANDS : creates
    AUTOMATION_RULES ||--o{ AUTOMATION_EXECUTIONS : evaluates
```

Ghi chú:

- `TelemetryRecords` giữ lịch sử; `DeviceLatestValues` tối ưu truy vấn dashboard.
- Dynamic value chỉ sử dụng đúng một cột number/boolean/string theo `ValueType`.
- `ProcessedMessages` độc lập để chống xử lý trùng MQTT QoS 1.
- `RealtimeOutbox` được SQL trigger tạo và API dispatcher phát qua SignalR.

## Application class diagram

```mermaid
classDiagram
    direction LR

    class MainViewModel {
        +NavigateCommand
        +HomeDeviceCards
        +AutomationRules
        +Notifications
        +ShutdownAsync()
    }

    class IoTPlatformApiClient {
        +LoginAsync()
        +GetDevicesAsync()
        +CreateCommandAsync()
        +CreateAutomationRuleAsync()
    }

    class DashboardRealtimeClient {
        +Changed
        +StartAsync()
        +StopAsync()
    }

    class AuthService {
        +RegisterAsync()
        +LoginAsync()
        +RefreshAsync()
        +LogoutAsync()
    }

    class DeviceClaimService {
        +ClaimAsync()
    }

    class DeviceManagementService {
        +ListAsync()
        +UpdateAsync()
        +UnclaimAsync()
    }

    class TelemetryQueryService {
        +GetLatestValuesAsync()
        +GetHistoryAsync()
    }

    class CommandService {
        +CreateManualAsync()
        +GetAsync()
    }

    class AutomationRuleService {
        +CreateAsync()
        +UpdateAsync()
        +DeleteAsync()
        +ListExecutionsAsync()
    }

    class NotificationService {
        +ListAsync()
        +MarkAllReadAsync()
    }

    class MqttWorker {
        +RunMqttSessionAsync()
        +PublishPendingCommandsAsync()
    }

    class DeviceMessageHandler {
        +HandleAsync(topic, payload)
    }

    class SqlTelemetryIngestionRepository {
        +IngestAsync()
        -EvaluateAutomationRulesAsync()
    }

    class SqlCommandDispatchRepository {
        +GetPendingAsync()
        +MarkSentAsync()
        +ApplyAcknowledgementAsync()
    }

    class SimulatedDeviceSession {
        +StartAsync()
        +StopAsync()
        -HandleCommandAsync()
        -PublishAsync()
    }

    class CapabilityDefinition {
        +Key
        +Kind
        +DataType
        +IsWritable
        +SupportedCommands
    }

    MainViewModel --> IoTPlatformApiClient
    MainViewModel --> DashboardRealtimeClient

    AuthService --> IAuthRepository
    DeviceClaimService --> IDeviceClaimRepository
    DeviceManagementService --> IDeviceManagementRepository
    TelemetryQueryService --> ITelemetryQueryRepository
    CommandService --> ICommandRepository
    AutomationRuleService --> IAutomationRuleRepository
    NotificationService --> INotificationRepository

    MqttWorker --> DeviceMessageHandler
    MqttWorker --> SqlCommandDispatchRepository
    DeviceMessageHandler --> SqlTelemetryIngestionRepository
    DeviceMessageHandler --> SqlCommandDispatchRepository
    SqlTelemetryIngestionRepository --> CapabilityDefinition
    SimulatedDeviceSession --> CapabilityDefinition
```

