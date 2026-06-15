using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;

namespace IoTPlatform.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabaseOptions(configuration);
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<ISqlTransactionScopeFactory, SqlTransactionScopeFactory>();
        services.AddSingleton<ISqlMigrationSource, EmbeddedSqlMigrationSource>();
        services.AddSingleton<ISqlMigrationRunner, SqlMigrationRunner>();
        return services;
    }

    public static IServiceCollection AddDatabaseOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.IoTPlatform), "ConnectionStrings:IoTPlatform is required.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddJwtOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(options => options.SigningKey.Length >= 32, "Jwt:SigningKey must contain at least 32 characters.")
            .Validate(options => options.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be positive.")
            .Validate(options => options.RefreshTokenDays > 0, "Jwt:RefreshTokenDays must be positive.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddHiveMqOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HiveMqOptions>()
            .Bind(configuration.GetSection(HiveMqOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "HiveMq:Host is required.")
            .Validate(options => options.Port is > 0 and <= 65535, "HiveMq:Port must be a valid TCP port.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "HiveMq:Username is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "HiveMq:Password is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientIdPrefix), "HiveMq:ClientIdPrefix is required.")
            .ValidateOnStart();

        return services;
    }
}
