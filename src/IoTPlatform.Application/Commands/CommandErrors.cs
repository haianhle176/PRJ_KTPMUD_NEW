namespace IoTPlatform.Application.Commands;

public class CommandValidationException(string message) : InvalidOperationException(message);

public sealed class CommandNotFoundException() : InvalidOperationException("Command was not found.");
