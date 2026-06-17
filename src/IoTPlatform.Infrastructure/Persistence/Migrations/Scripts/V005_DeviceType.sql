IF COL_LENGTH('dbo.Devices', 'DeviceType') IS NULL
BEGIN
    ALTER TABLE dbo.Devices
    ADD DeviceType nvarchar(64) NOT NULL
        CONSTRAINT DF_Devices_DeviceType DEFAULT 'custom';
END;
GO

UPDATE dbo.Devices
SET DeviceType = 'custom'
WHERE DeviceType IS NULL OR LTRIM(RTRIM(DeviceType)) = '';
GO
