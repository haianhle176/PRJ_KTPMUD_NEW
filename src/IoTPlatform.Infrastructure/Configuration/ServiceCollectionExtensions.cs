using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using IoTPlatform.Infrastructure.Realtime;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Application.Auth;
using IoTPlatform.Application.Audit;
using IoTPlatform.Application.Devices;
using IoTPlatform.Application.Security;
using IoTPlatform.Infrastructure.Audit;
using IoTPlatform.Infrastructure.Auth;
using IoTPlatform.Application.Telemetry;
using IoTPlatform.Application.Commands;
using IoTPlatform.Application.Automation;
using IoTPlatform.Infrastructure.Automation;
using IoTPlatform.Infrastructure.Commands;
using IoTPlatform.Infrastructure.Telemetry;
using IoTPlatform.Application.Notifications;
using IoTPlatform.Infrastructure.Notifications;
using IoTPlatform.Infrastructure.Operations;

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
        services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.AddScoped<IAuthRepository, SqlAuthRepository>();
        services.AddScoped<IDeviceClaimRepository, SqlDeviceClaimRepository>();
        services.AddScoped<IDeviceManagementRepository, SqlDeviceManagementRepository>();
        services.AddScoped<ITelemetryQueryRepository, SqlTelemetryQueryRepository>();
        services.AddScoped<ICommandRepository, SqlCommandRepository>();
        services.AddScoped<IAutomationRuleRepository, SqlAutomationRuleRepository>();
        services.AddSingleton<IScheduledAutomationRepository, SqlScheduledAutomationRepository>();
        services.AddScoped<INotificationRepository, SqlNotificationRepository>();
        services.AddSingleton<ITelemetryIngestionRepository, SqlTelemetryIngestionRepository>();
        services.AddSingleton<ICommandDispatchRepository, SqlCommandDispatchRepository>();
        services.AddSingleton<IOperationsMaintenanceRepository, SqlOperationsMaintenanceRepository>();
        services.AddSingleton<IRealtimeOutboxRepository, SqlRealtimeOutboxRepository>();
        services.AddScoped<IAuditLogRepository, SqlAuditLogRepository>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDeviceClaimService, DeviceClaimService>();
        services.AddScoped<IDeviceManagementService, DeviceManagementService>();
        services.AddScoped<ITelemetryQueryService, TelemetryQueryService>();
        services.AddScoped<ICommandService, CommandService>();
        services.AddScoped<IAutomationRuleService, AutomationRuleService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ICurrentUserTokenService, CurrentUserTokenService>();
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
