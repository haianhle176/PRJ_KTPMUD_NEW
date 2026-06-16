using IoTPlatform.Application.Auth;
using IoTPlatform.Infrastructure.Auth;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Dapper;

namespace IoTPlatform.Infrastructure.Tests.Auth;

public sealed class AuthServiceIntegrationTests
{
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=false;TrustServerCertificate=true";

    [Fact]
    public async Task RegisterLoginRefreshAndLogout_WorkEndToEnd()
    {
        var databaseName = $"IoTPlatform_Auth_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var service = await CreateServiceAsync(connectionString);

            var registered = await service.RegisterAsync(new RegisterRequestDto(
                "student@example.test",
                "Password123!",
                "Student One"));
            Assert.NotEqual(Guid.Empty, registered.User.Id);
            Assert.False(string.IsNullOrWhiteSpace(registered.Tokens.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(registered.Tokens.RefreshToken));

            await using (var connection = new SqlConnection(connectionString))
            {
                var passwordHash = await connection.ExecuteScalarAsync<string>(
                    "SELECT PasswordHash FROM dbo.Users WHERE NormalizedEmail = 'STUDENT@EXAMPLE.TEST';");
                Assert.NotEqual("Password123!", passwordHash);
            }

            await Assert.ThrowsAsync<DuplicateEmailException>(() => service.RegisterAsync(new RegisterRequestDto(
                "STUDENT@example.test",
                "Password123!",
                "Student Duplicate")));

            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new LoginRequestDto(
                "student@example.test",
                "wrong-password")));

            var loggedIn = await service.LoginAsync(new LoginRequestDto("student@example.test", "Password123!"));
            var refreshed = await service.RefreshAsync(new RefreshTokenRequestDto(loggedIn.Tokens.RefreshToken));
            Assert.NotEqual(loggedIn.Tokens.RefreshToken, refreshed.Tokens.RefreshToken);
            await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RefreshAsync(new RefreshTokenRequestDto(loggedIn.Tokens.RefreshToken)));

            await service.LogoutAsync(new LogoutRequestDto(refreshed.Tokens.RefreshToken));
            await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RefreshAsync(new RefreshTokenRequestDto(refreshed.Tokens.RefreshToken)));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static async Task<IAuthService> CreateServiceAsync(string connectionString)
    {
        var databaseOptions = Options.Create(new DatabaseOptions { IoTPlatform = connectionString });
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "IoTPlatform.Tests",
            Audience = "IoTPlatform.Tests",
            SigningKey = "test-signing-key-that-is-long-enough-123456",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30
        });
        var connectionFactory = new SqlConnectionFactory(databaseOptions);
        await new SqlMigrationRunner(connectionFactory, new EmbeddedSqlMigrationSource()).MigrateAsync();
        var transactionFactory = new SqlTransactionScopeFactory(connectionFactory);
        var repository = new SqlAuthRepository(transactionFactory, connectionFactory);
        return new AuthService(repository, new PasswordService(), new JwtTokenService(jwtOptions));
    }

    private static async Task ExecuteMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql);
    }
}
