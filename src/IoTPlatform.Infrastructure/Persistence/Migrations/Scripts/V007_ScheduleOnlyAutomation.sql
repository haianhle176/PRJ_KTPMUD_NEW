ALTER TABLE dbo.AutomationRules DROP CONSTRAINT FK_AutomationRules_SourceCapability;
GO

ALTER TABLE dbo.AutomationRules DROP CONSTRAINT CK_AutomationRules_ComparisonOperator;
GO

ALTER TABLE dbo.AutomationRules DROP CONSTRAINT CK_AutomationRules_ThresholdValueType;
GO

ALTER TABLE dbo.AutomationRules DROP CONSTRAINT CK_AutomationRules_DifferentCapabilities;
GO

ALTER TABLE dbo.AutomationRules ALTER COLUMN SourceCapabilityId uniqueidentifier NULL;
ALTER TABLE dbo.AutomationRules ALTER COLUMN ComparisonOperator varchar(4) NULL;
ALTER TABLE dbo.AutomationRules ALTER COLUMN ThresholdValueType varchar(16) NULL;
GO

ALTER TABLE dbo.AutomationRules
ADD CONSTRAINT FK_AutomationRules_SourceCapability FOREIGN KEY (SourceCapabilityId) REFERENCES dbo.Capabilities(Id);
GO

ALTER TABLE dbo.AutomationRules
ADD CONSTRAINT CK_AutomationRules_ComparisonOperator CHECK (ComparisonOperator IS NULL OR ComparisonOperator IN ('>', '>=', '<', '<=', '==', '!='));
GO

ALTER TABLE dbo.AutomationRules
ADD CONSTRAINT CK_AutomationRules_ThresholdValueType CHECK (ThresholdValueType IS NULL OR ThresholdValueType IN ('number', 'boolean', 'string'));
GO

ALTER TABLE dbo.AutomationRules
ADD CONSTRAINT CK_AutomationRules_DifferentCapabilities CHECK (SourceCapabilityId IS NULL OR SourceCapabilityId <> TargetCapabilityId);
GO

ALTER TABLE dbo.AutomationRules
ADD CONSTRAINT CK_AutomationRules_TriggerShape CHECK (
    (SourceCapabilityId IS NOT NULL AND ComparisonOperator IS NOT NULL AND ThresholdValueType IS NOT NULL)
    OR
    (SourceCapabilityId IS NULL AND ComparisonOperator IS NULL AND ThresholdValueType IS NULL)
);
GO

ALTER TABLE dbo.AutomationExecutions DROP CONSTRAINT UQ_AutomationExecutions_Rule_Message;
GO

ALTER TABLE dbo.AutomationExecutions ALTER COLUMN TriggerMessageId uniqueidentifier NULL;
GO

CREATE UNIQUE INDEX UX_AutomationExecutions_Rule_Message
ON dbo.AutomationExecutions(AutomationRuleId, TriggerMessageId)
WHERE TriggerMessageId IS NOT NULL;
GO
