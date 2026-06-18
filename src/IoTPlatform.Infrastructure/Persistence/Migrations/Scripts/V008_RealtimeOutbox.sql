CREATE TABLE dbo.RealtimeOutbox
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RealtimeOutbox PRIMARY KEY,
    EventId uniqueidentifier NOT NULL,
    OwnerUserId uniqueidentifier NULL,
    Kind varchar(32) NOT NULL,
    DeviceId uniqueidentifier NULL,
    EntityId nvarchar(64) NULL,
    OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_RealtimeOutbox_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
    DispatchedAtUtc datetime2(3) NULL,
    AttemptCount int NOT NULL CONSTRAINT DF_RealtimeOutbox_AttemptCount DEFAULT 0,
    NextAttemptAtUtc datetime2(3) NOT NULL CONSTRAINT DF_RealtimeOutbox_NextAttemptAtUtc DEFAULT SYSUTCDATETIME(),
    LastError nvarchar(1000) NULL,
    CONSTRAINT UQ_RealtimeOutbox_EventId UNIQUE (EventId),
    CONSTRAINT FK_RealtimeOutbox_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT CK_RealtimeOutbox_Kind CHECK (Kind IN ('telemetry', 'presence', 'command', 'notification', 'automation', 'device', 'availableDevices'))
);
CREATE INDEX IX_RealtimeOutbox_Pending ON dbo.RealtimeOutbox(DispatchedAtUtc, NextAttemptAtUtc, Id)
    INCLUDE (EventId, OwnerUserId, Kind, DeviceId, EntityId, OccurredAtUtc, AttemptCount);
GO

CREATE TRIGGER dbo.TR_DeviceLatestValues_Realtime
ON dbo.DeviceLatestValues
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind, DeviceId, EntityId, OccurredAtUtc)
    SELECT NEWID(), d.OwnerUserId, 'telemetry', i.DeviceId, CONVERT(nvarchar(64), i.CapabilityId), i.RecordedAtUtc
    FROM inserted i
    INNER JOIN dbo.Devices d ON d.Id = i.DeviceId
    WHERE d.OwnerUserId IS NOT NULL;
END;
GO

CREATE TRIGGER dbo.TR_Devices_Realtime
ON dbo.Devices
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind, DeviceId, EntityId)
    SELECT NEWID(), COALESCE(i.OwnerUserId, d.OwnerUserId), 'device', COALESCE(i.Id, d.Id), CONVERT(nvarchar(64), COALESCE(i.Id, d.Id))
    FROM inserted i
    FULL OUTER JOIN deleted d ON d.Id = i.Id
    WHERE COALESCE(i.OwnerUserId, d.OwnerUserId) IS NOT NULL
      AND (d.Id IS NULL OR i.Id IS NULL
           OR ISNULL(i.OwnerUserId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.OwnerUserId, '00000000-0000-0000-0000-000000000000')
           OR ISNULL(i.DisplayName, '') <> ISNULL(d.DisplayName, '')
           OR ISNULL(i.Category, '') <> ISNULL(d.Category, ''));

    INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind, DeviceId, EntityId)
    SELECT NEWID(), COALESCE(i.OwnerUserId, d.OwnerUserId), 'presence', COALESCE(i.Id, d.Id), CONVERT(nvarchar(64), COALESCE(i.Id, d.Id))
    FROM inserted i
    INNER JOIN deleted d ON d.Id = i.Id
    WHERE i.OwnerUserId IS NOT NULL AND ISNULL(i.Status, '') <> ISNULL(d.Status, '');

    IF EXISTS (
        SELECT 1 FROM inserted i FULL OUTER JOIN deleted d ON d.Id = i.Id
        WHERE i.Id IS NULL OR d.Id IS NULL
           OR ISNULL(i.OwnerUserId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.OwnerUserId, '00000000-0000-0000-0000-000000000000')
           OR (i.OwnerUserId IS NULL AND (ISNULL(i.Status, '') <> ISNULL(d.Status, '') OR ISNULL(i.ManifestVersion, 0) <> ISNULL(d.ManifestVersion, 0)))
    )
        INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind) VALUES (NEWID(), NULL, 'availableDevices');
END;
GO

CREATE TRIGGER dbo.TR_PairingCodes_Realtime
ON dbo.PairingCodes
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        FULL OUTER JOIN deleted d ON d.Id = i.Id
        INNER JOIN dbo.Devices device ON device.Id = COALESCE(i.DeviceId, d.DeviceId)
        WHERE device.OwnerUserId IS NULL
    )
        INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind) VALUES (NEWID(), NULL, 'availableDevices');
END;
GO

CREATE TRIGGER dbo.TR_DeviceCommands_Realtime
ON dbo.DeviceCommands
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind, DeviceId, EntityId)
    SELECT NEWID(), i.OwnerUserId, 'command', i.DeviceId, CONVERT(nvarchar(64), i.Id)
    FROM inserted i
    LEFT JOIN deleted d ON d.Id = i.Id
    WHERE d.Id IS NULL OR i.Status <> d.Status;
END;
GO

CREATE TRIGGER dbo.TR_Notifications_Realtime
ON dbo.Notifications
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind, DeviceId, EntityId)
    SELECT NEWID(), COALESCE(i.OwnerUserId, d.OwnerUserId), 'notification', COALESCE(i.DeviceId, d.DeviceId), CONVERT(nvarchar(64), COALESCE(i.Id, d.Id))
    FROM inserted i FULL OUTER JOIN deleted d ON d.Id = i.Id;
END;
GO

CREATE TRIGGER dbo.TR_AutomationRules_Realtime
ON dbo.AutomationRules
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.RealtimeOutbox (EventId, OwnerUserId, Kind, EntityId)
    SELECT NEWID(), COALESCE(i.OwnerUserId, d.OwnerUserId), 'automation', CONVERT(nvarchar(64), COALESCE(i.Id, d.Id))
    FROM inserted i FULL OUTER JOIN deleted d ON d.Id = i.Id;
END;
GO
