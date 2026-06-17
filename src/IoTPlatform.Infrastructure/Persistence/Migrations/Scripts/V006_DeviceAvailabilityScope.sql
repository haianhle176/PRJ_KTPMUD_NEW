IF COL_LENGTH('dbo.Devices', 'AvailableToUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Devices
    ADD AvailableToUserId uniqueidentifier NULL;

    ALTER TABLE dbo.Devices
    ADD CONSTRAINT FK_Devices_AvailableToUsers FOREIGN KEY (AvailableToUserId) REFERENCES dbo.Users(Id);

    CREATE INDEX IX_Devices_AvailableToUserId_Status
    ON dbo.Devices(AvailableToUserId, Status)
    INCLUDE (Serial, DeviceType, ManifestVersion, LastSeenAtUtc);
END;
GO
