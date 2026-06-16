ALTER TABLE dbo.AutomationRules
ADD ActiveFromTime time(0) NULL,
    ActiveToTime time(0) NULL,
    ActiveDaysOfWeek tinyint NULL;
GO

ALTER TABLE dbo.AutomationRules
ADD CONSTRAINT CK_AutomationRules_ActiveDaysOfWeek CHECK (ActiveDaysOfWeek IS NULL OR ActiveDaysOfWeek BETWEEN 1 AND 127);
GO

CREATE TABLE dbo.Notifications
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    OwnerUserId uniqueidentifier NOT NULL,
    DeviceId uniqueidentifier NULL,
    CapabilityId uniqueidentifier NULL,
    Type nvarchar(64) NOT NULL,
    Severity varchar(16) NOT NULL,
    Title nvarchar(160) NOT NULL,
    Message nvarchar(1000) NOT NULL,
    IsRead bit NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Notifications_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Notifications_Devices FOREIGN KEY (DeviceId) REFERENCES dbo.Devices(Id),
    CONSTRAINT FK_Notifications_Capabilities FOREIGN KEY (CapabilityId) REFERENCES dbo.Capabilities(Id),
    CONSTRAINT CK_Notifications_Severity CHECK (Severity IN ('info', 'warning', 'critical'))
);
CREATE INDEX IX_Notifications_OwnerUserId_IsRead_CreatedAtUtc ON dbo.Notifications(OwnerUserId, IsRead, CreatedAtUtc DESC);
CREATE INDEX IX_Notifications_DeviceId_CreatedAtUtc ON dbo.Notifications(DeviceId, CreatedAtUtc DESC);
GO

CREATE TABLE dbo.TelemetryHourlyAggregates
(
    CapabilityId uniqueidentifier NOT NULL,
    BucketStartUtc datetime2(0) NOT NULL,
    DeviceId uniqueidentifier NOT NULL,
    SampleCount int NOT NULL,
    MinNumberValue decimal(18,6) NULL,
    MaxNumberValue decimal(18,6) NULL,
    AvgNumberValue decimal(18,6) NULL,
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_TelemetryHourlyAggregates_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TelemetryHourlyAggregates PRIMARY KEY (CapabilityId, BucketStartUtc),
    CONSTRAINT FK_TelemetryHourlyAggregates_Capabilities FOREIGN KEY (CapabilityId) REFERENCES dbo.Capabilities(Id),
    CONSTRAINT FK_TelemetryHourlyAggregates_Devices FOREIGN KEY (DeviceId) REFERENCES dbo.Devices(Id)
);
CREATE INDEX IX_TelemetryHourlyAggregates_DeviceId_BucketStartUtc ON dbo.TelemetryHourlyAggregates(DeviceId, BucketStartUtc DESC);
GO
