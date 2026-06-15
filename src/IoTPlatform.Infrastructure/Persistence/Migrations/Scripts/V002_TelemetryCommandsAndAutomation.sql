CREATE TABLE dbo.TelemetryRecords
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TelemetryRecords PRIMARY KEY,
    MessageId uniqueidentifier NOT NULL,
    DeviceId uniqueidentifier NOT NULL,
    CapabilityId uniqueidentifier NOT NULL,
    ValueType varchar(16) NOT NULL,
    NumberValue decimal(18,6) NULL,
    BooleanValue bit NULL,
    StringValue nvarchar(1000) NULL,
    RecordedAtUtc datetime2(3) NOT NULL,
    ReceivedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_TelemetryRecords_ReceivedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_TelemetryRecords_Devices FOREIGN KEY (DeviceId) REFERENCES dbo.Devices(Id),
    CONSTRAINT FK_TelemetryRecords_Capabilities FOREIGN KEY (CapabilityId) REFERENCES dbo.Capabilities(Id),
    CONSTRAINT UQ_TelemetryRecords_MessageId_CapabilityId UNIQUE (MessageId, CapabilityId),
    CONSTRAINT CK_TelemetryRecords_ValueType CHECK (ValueType IN ('number', 'boolean', 'string')),
    CONSTRAINT CK_TelemetryRecords_ExactlyOneValue CHECK (
        (ValueType = 'number' AND NumberValue IS NOT NULL AND BooleanValue IS NULL AND StringValue IS NULL) OR
        (ValueType = 'boolean' AND NumberValue IS NULL AND BooleanValue IS NOT NULL AND StringValue IS NULL) OR
        (ValueType = 'string' AND NumberValue IS NULL AND BooleanValue IS NULL AND StringValue IS NOT NULL))
);
CREATE INDEX IX_TelemetryRecords_CapabilityId_RecordedAtUtc ON dbo.TelemetryRecords(CapabilityId, RecordedAtUtc DESC) INCLUDE (ValueType, NumberValue, BooleanValue, StringValue);
CREATE INDEX IX_TelemetryRecords_DeviceId_RecordedAtUtc ON dbo.TelemetryRecords(DeviceId, RecordedAtUtc DESC);
GO

CREATE TABLE dbo.DeviceLatestValues
(
    CapabilityId uniqueidentifier NOT NULL CONSTRAINT PK_DeviceLatestValues PRIMARY KEY,
    DeviceId uniqueidentifier NOT NULL,
    MessageId uniqueidentifier NOT NULL,
    ValueType varchar(16) NOT NULL,
    NumberValue decimal(18,6) NULL,
    BooleanValue bit NULL,
    StringValue nvarchar(1000) NULL,
    RecordedAtUtc datetime2(3) NOT NULL,
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DeviceLatestValues_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_DeviceLatestValues_Capabilities FOREIGN KEY (CapabilityId) REFERENCES dbo.Capabilities(Id),
    CONSTRAINT FK_DeviceLatestValues_Devices FOREIGN KEY (DeviceId) REFERENCES dbo.Devices(Id),
    CONSTRAINT CK_DeviceLatestValues_ValueType CHECK (ValueType IN ('number', 'boolean', 'string')),
    CONSTRAINT CK_DeviceLatestValues_ExactlyOneValue CHECK (
        (ValueType = 'number' AND NumberValue IS NOT NULL AND BooleanValue IS NULL AND StringValue IS NULL) OR
        (ValueType = 'boolean' AND NumberValue IS NULL AND BooleanValue IS NOT NULL AND StringValue IS NULL) OR
        (ValueType = 'string' AND NumberValue IS NULL AND BooleanValue IS NULL AND StringValue IS NOT NULL))
);
CREATE INDEX IX_DeviceLatestValues_DeviceId ON dbo.DeviceLatestValues(DeviceId);
GO

CREATE TABLE dbo.DeviceCommands
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_DeviceCommands PRIMARY KEY,
    DeviceId uniqueidentifier NOT NULL,
    CapabilityId uniqueidentifier NOT NULL,
    OwnerUserId uniqueidentifier NOT NULL,
    AutomationRuleId uniqueidentifier NULL,
    CommandName nvarchar(32) NOT NULL,
    DesiredValueType varchar(16) NOT NULL,
    DesiredNumberValue decimal(18,6) NULL,
    DesiredBooleanValue bit NULL,
    DesiredStringValue nvarchar(1000) NULL,
    Source varchar(16) NOT NULL,
    Status varchar(16) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DeviceCommands_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    SentAtUtc datetime2(3) NULL,
    AcceptedAtUtc datetime2(3) NULL,
    CompletedAtUtc datetime2(3) NULL,
    ExpiresAtUtc datetime2(3) NOT NULL,
    FailureCode nvarchar(64) NULL,
    FailureMessage nvarchar(500) NULL,
    CONSTRAINT FK_DeviceCommands_Devices FOREIGN KEY (DeviceId) REFERENCES dbo.Devices(Id),
    CONSTRAINT FK_DeviceCommands_Capabilities FOREIGN KEY (CapabilityId) REFERENCES dbo.Capabilities(Id),
    CONSTRAINT FK_DeviceCommands_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_DeviceCommands_ValueType CHECK (DesiredValueType IN ('number', 'boolean', 'string')),
    CONSTRAINT CK_DeviceCommands_Source CHECK (Source IN ('manual', 'automation')),
    CONSTRAINT CK_DeviceCommands_Status CHECK (Status IN ('pending', 'sent', 'accepted', 'completed', 'failed', 'timed_out')),
    CONSTRAINT CK_DeviceCommands_ExactlyOneValue CHECK (
        (DesiredValueType = 'number' AND DesiredNumberValue IS NOT NULL AND DesiredBooleanValue IS NULL AND DesiredStringValue IS NULL) OR
        (DesiredValueType = 'boolean' AND DesiredNumberValue IS NULL AND DesiredBooleanValue IS NOT NULL AND DesiredStringValue IS NULL) OR
        (DesiredValueType = 'string' AND DesiredNumberValue IS NULL AND DesiredBooleanValue IS NULL AND DesiredStringValue IS NOT NULL))
);
CREATE INDEX IX_DeviceCommands_DeviceId_Status_CreatedAtUtc ON dbo.DeviceCommands(DeviceId, Status, CreatedAtUtc DESC);
CREATE INDEX IX_DeviceCommands_OwnerUserId_CreatedAtUtc ON dbo.DeviceCommands(OwnerUserId, CreatedAtUtc DESC);
GO

CREATE TABLE dbo.CommandAcknowledgements
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommandAcknowledgements PRIMARY KEY,
    MessageId uniqueidentifier NOT NULL,
    CommandId uniqueidentifier NOT NULL,
    Status varchar(16) NOT NULL,
    ReportedValueType varchar(16) NULL,
    ReportedNumberValue decimal(18,6) NULL,
    ReportedBooleanValue bit NULL,
    ReportedStringValue nvarchar(1000) NULL,
    ErrorCode nvarchar(64) NULL,
    ErrorMessage nvarchar(500) NULL,
    ReceivedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_CommandAcknowledgements_ReceivedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_CommandAcknowledgements_DeviceCommands FOREIGN KEY (CommandId) REFERENCES dbo.DeviceCommands(Id),
    CONSTRAINT UQ_CommandAcknowledgements_MessageId UNIQUE (MessageId),
    CONSTRAINT CK_CommandAcknowledgements_Status CHECK (Status IN ('accepted', 'completed', 'failed')),
    CONSTRAINT CK_CommandAcknowledgements_ValueType CHECK (ReportedValueType IS NULL OR ReportedValueType IN ('number', 'boolean', 'string'))
);
CREATE INDEX IX_CommandAcknowledgements_CommandId_ReceivedAtUtc ON dbo.CommandAcknowledgements(CommandId, ReceivedAtUtc);
GO

CREATE TABLE dbo.AutomationRules
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_AutomationRules PRIMARY KEY,
    OwnerUserId uniqueidentifier NOT NULL,
    Name nvarchar(100) NOT NULL,
    SourceCapabilityId uniqueidentifier NOT NULL,
    ComparisonOperator varchar(4) NOT NULL,
    ThresholdValueType varchar(16) NOT NULL,
    ThresholdNumberValue decimal(18,6) NULL,
    ThresholdBooleanValue bit NULL,
    ThresholdStringValue nvarchar(1000) NULL,
    TargetCapabilityId uniqueidentifier NOT NULL,
    DesiredValueType varchar(16) NOT NULL,
    DesiredNumberValue decimal(18,6) NULL,
    DesiredBooleanValue bit NULL,
    DesiredStringValue nvarchar(1000) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_AutomationRules_IsEnabled DEFAULT 1,
    CooldownSeconds int NOT NULL CONSTRAINT DF_AutomationRules_CooldownSeconds DEFAULT 30,
    LastConditionResult bit NULL,
    LastTriggeredAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AutomationRules_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AutomationRules_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AutomationRules_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_AutomationRules_SourceCapability FOREIGN KEY (SourceCapabilityId) REFERENCES dbo.Capabilities(Id),
    CONSTRAINT FK_AutomationRules_TargetCapability FOREIGN KEY (TargetCapabilityId) REFERENCES dbo.Capabilities(Id),
    CONSTRAINT CK_AutomationRules_ComparisonOperator CHECK (ComparisonOperator IN ('>', '>=', '<', '<=', '==', '!=')),
    CONSTRAINT CK_AutomationRules_ThresholdValueType CHECK (ThresholdValueType IN ('number', 'boolean', 'string')),
    CONSTRAINT CK_AutomationRules_DesiredValueType CHECK (DesiredValueType IN ('number', 'boolean', 'string')),
    CONSTRAINT CK_AutomationRules_CooldownSeconds CHECK (CooldownSeconds >= 0),
    CONSTRAINT CK_AutomationRules_DifferentCapabilities CHECK (SourceCapabilityId <> TargetCapabilityId)
);
CREATE INDEX IX_AutomationRules_OwnerUserId_IsEnabled ON dbo.AutomationRules(OwnerUserId, IsEnabled);
CREATE INDEX IX_AutomationRules_SourceCapabilityId_IsEnabled ON dbo.AutomationRules(SourceCapabilityId, IsEnabled);
GO

ALTER TABLE dbo.DeviceCommands ADD CONSTRAINT FK_DeviceCommands_AutomationRules FOREIGN KEY (AutomationRuleId) REFERENCES dbo.AutomationRules(Id);
GO

CREATE TABLE dbo.AutomationExecutions
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AutomationExecutions PRIMARY KEY,
    AutomationRuleId uniqueidentifier NOT NULL,
    TriggerMessageId uniqueidentifier NOT NULL,
    CommandId uniqueidentifier NULL,
    Outcome varchar(16) NOT NULL,
    Reason nvarchar(500) NULL,
    EvaluatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AutomationExecutions_EvaluatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AutomationExecutions_Rules FOREIGN KEY (AutomationRuleId) REFERENCES dbo.AutomationRules(Id),
    CONSTRAINT FK_AutomationExecutions_Commands FOREIGN KEY (CommandId) REFERENCES dbo.DeviceCommands(Id),
    CONSTRAINT UQ_AutomationExecutions_Rule_Message UNIQUE (AutomationRuleId, TriggerMessageId),
    CONSTRAINT CK_AutomationExecutions_Outcome CHECK (Outcome IN ('triggered', 'skipped', 'failed'))
);
CREATE INDEX IX_AutomationExecutions_RuleId_EvaluatedAtUtc ON dbo.AutomationExecutions(AutomationRuleId, EvaluatedAtUtc DESC);
GO

CREATE TABLE dbo.ProcessedMessages
(
    MessageId uniqueidentifier NOT NULL CONSTRAINT PK_ProcessedMessages PRIMARY KEY,
    Topic nvarchar(256) NOT NULL,
    MessageType varchar(32) NOT NULL,
    ProcessedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ProcessedMessages_ProcessedAtUtc DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_ProcessedMessages_ProcessedAtUtc ON dbo.ProcessedMessages(ProcessedAtUtc);
GO

CREATE TABLE dbo.AuditLogs
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
    UserId uniqueidentifier NULL,
    Action nvarchar(100) NOT NULL,
    EntityType nvarchar(64) NOT NULL,
    EntityId nvarchar(64) NULL,
    CorrelationId nvarchar(64) NULL,
    DetailsJson nvarchar(max) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AuditLogs_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_AuditLogs_DetailsJson CHECK (DetailsJson IS NULL OR ISJSON(DetailsJson) = 1)
);
CREATE INDEX IX_AuditLogs_UserId_CreatedAtUtc ON dbo.AuditLogs(UserId, CreatedAtUtc DESC);
CREATE INDEX IX_AuditLogs_EntityType_EntityId_CreatedAtUtc ON dbo.AuditLogs(EntityType, EntityId, CreatedAtUtc DESC);
GO
