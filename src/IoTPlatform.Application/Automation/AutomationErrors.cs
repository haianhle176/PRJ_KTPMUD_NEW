namespace IoTPlatform.Application.Automation;

public class AutomationValidationException(string message) : InvalidOperationException(message);

public sealed class AutomationRuleNotFoundException() : InvalidOperationException("Automation rule was not found.");
