CREATE TABLE dbo.Users
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email nvarchar(320) NOT NULL,
    NormalizedEmail nvarchar(320) NOT NULL,
    DisplayName nvarchar(100) NOT NULL,
    PasswordHash nvarchar(500) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Users_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Users_NormalizedEmail UNIQUE (NormalizedEmail)
);
GO

CREATE TABLE dbo.RefreshTokens
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId uniqueidentifier NOT NULL,
    TokenHash char(64) NOT NULL,
    ExpiresAtUtc datetime2(3) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    RevokedAtUtc datetime2(3) NULL,
    ReplacedByTokenId uniqueidentifier NULL,
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_RefreshTokens_ReplacedBy FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.RefreshTokens(Id),
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash)
);
CREATE INDEX IX_RefreshTokens_UserId_ExpiresAtUtc ON dbo.RefreshTokens(UserId, ExpiresAtUtc DESC);
GO

CREATE TABLE dbo.Devices
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Devices PRIMARY KEY,
    Serial nvarchar(64) NOT NULL,
    OwnerUserId uniqueidentifier NULL,
    DisplayName nvarchar(100) NULL,
    Category nvarchar(64) NULL,
    Status varchar(16) NOT NULL CONSTRAINT DF_Devices_Status DEFAULT 'offline',
    ManifestVersion int NOT NULL CONSTRAINT DF_Devices_ManifestVersion DEFAULT 0,
    LastSeenAtUtc datetime2(3) NULL,
    ClaimedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Devices_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Devices_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Devices_Users FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT UQ_Devices_Serial UNIQUE (Serial),
    CONSTRAINT CK_Devices_Status CHECK (Status IN ('online', 'offline', 'revoked')),
    CONSTRAINT CK_Devices_ManifestVersion CHECK (ManifestVersion >= 0)
);
CREATE INDEX IX_Devices_OwnerUserId_Status ON dbo.Devices(OwnerUserId, Status) INCLUDE (DisplayName, Serial, LastSeenAtUtc);
GO

CREATE TABLE dbo.PairingCodes
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_PairingCodes PRIMARY KEY,
    DeviceId uniqueidentifier NOT NULL,
    CodeHash char(64) NOT NULL,
    ExpiresAtUtc datetime2(3) NOT NULL,
    UsedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_PairingCodes_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_PairingCodes_Devices FOREIGN KEY (DeviceId) REFERENCES dbo.Devices(Id),
    CONSTRAINT UQ_PairingCodes_CodeHash UNIQUE (CodeHash)
);
CREATE INDEX IX_PairingCodes_DeviceId_ExpiresAtUtc ON dbo.PairingCodes(DeviceId, ExpiresAtUtc DESC) INCLUDE (UsedAtUtc);
GO

CREATE TABLE dbo.Capabilities
(
    Id uniqueidentifier NOT NULL CONSTRAINT PK_Capabilities PRIMARY KEY,
    DeviceId uniqueidentifier NOT NULL,
    [Key] nvarchar(64) NOT NULL,
    DisplayName nvarchar(100) NOT NULL,
    Kind varchar(16) NOT NULL,
    DataType varchar(16) NOT NULL,
    Unit nvarchar(32) NULL,
    MinValue decimal(18,6) NULL,
    MaxValue decimal(18,6) NULL,
    IsWritable bit NOT NULL,
    SupportedCommandsJson nvarchar(max) NOT NULL CONSTRAINT DF_Capabilities_SupportedCommands DEFAULT '[]',
    IsActive bit NOT NULL CONSTRAINT DF_Capabilities_IsActive DEFAULT 1,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Capabilities_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Capabilities_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Capabilities_Devices FOREIGN KEY (DeviceId) REFERENCES dbo.Devices(Id),
    CONSTRAINT UQ_Capabilities_DeviceId_Key UNIQUE (DeviceId, [Key]),
    CONSTRAINT CK_Capabilities_Kind CHECK (Kind IN ('sensor', 'actuator')),
    CONSTRAINT CK_Capabilities_DataType CHECK (DataType IN ('number', 'boolean', 'string')),
    CONSTRAINT CK_Capabilities_NumberRange CHECK ((DataType = 'number') OR (MinValue IS NULL AND MaxValue IS NULL)),
    CONSTRAINT CK_Capabilities_MinMax CHECK (MinValue IS NULL OR MaxValue IS NULL OR MinValue <= MaxValue),
    CONSTRAINT CK_Capabilities_SensorWritable CHECK (Kind <> 'sensor' OR IsWritable = 0),
    CONSTRAINT CK_Capabilities_SupportedCommandsJson CHECK (ISJSON(SupportedCommandsJson) = 1)
);
CREATE INDEX IX_Capabilities_DeviceId_IsActive ON dbo.Capabilities(DeviceId, IsActive) INCLUDE ([Key], Kind, DataType);
GO
