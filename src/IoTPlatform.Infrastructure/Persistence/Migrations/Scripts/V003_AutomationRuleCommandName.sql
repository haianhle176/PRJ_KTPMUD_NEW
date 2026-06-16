ALTER TABLE dbo.AutomationRules
ADD CommandName nvarchar(32) NOT NULL CONSTRAINT DF_AutomationRules_CommandName DEFAULT 'SET';
GO
