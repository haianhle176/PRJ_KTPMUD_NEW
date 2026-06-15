namespace IoTPlatform.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string IoTPlatform { get; init; } = string.Empty;
}
