using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.DeviceApp.Services;

public sealed class DeviceAppApiClient
{
    private readonly HttpClient _httpClient;
    private readonly DeviceAppTokenStore _tokenStore = new();
    private DeviceAppAuthTokens? _tokens;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeviceAppApiClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://omniconnect-b5gmdffba4a4dkeq.southeastasia-01.azurewebsites.net/")
        };
    }

    public bool IsAuthenticated => _tokens is not null;

    public async Task<bool> TryRestoreAsync()
    {
        _tokens = await _tokenStore.LoadAsync();
        return _tokens is not null;
    }

    public async Task<AuthenticatedUser> LoginAsync(string email, string password)
    {
        var result = await PostAnonymousAsync<AuthResult>("api/v1/auth/login", new { email, password });
        await SetTokensAsync(result.Tokens);
        return result.User;
    }

    public async Task LogoutAsync()
    {
        if (_tokens is not null)
        {
            await SendAsync(HttpMethod.Post, "api/v1/auth/logout", new { _tokens.RefreshToken }, allowRefresh: false);
        }

        _tokens = null;
        _tokenStore.Clear();
    }

    public Task<DeviceSummary> ProvisionDeviceAsync(
        string serial,
        string displayName,
        string? category,
        string deviceType,
        int manifestVersion,
        IReadOnlyList<CapabilityManifestItem> capabilities) =>
        PostAsync<DeviceSummary>("api/v1/devices/provision", new
        {
            serial,
            displayName,
            category,
            deviceType,
            manifestVersion,
            capabilities = capabilities.Select(capability => new
            {
                capability.Key,
                capability.DisplayName,
                capability.Kind,
                capability.DataType,
                capability.Unit,
                capability.MinValue,
                capability.MaxValue,
                capability.IsWritable,
                capability.SupportedCommands
            })
        });

    private async Task SetTokensAsync(TokenResponse tokens)
    {
        _tokens = new DeviceAppAuthTokens(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshTokenExpiresAtUtc);
        await _tokenStore.SaveAsync(_tokens);
    }

    private async Task<T> PostAsync<T>(string path, object body) => await ReadAsync<T>(await SendAsync(HttpMethod.Post, path, body));

    private async Task<T> PostAnonymousAsync<T>(string path, object body)
    {
        var response = await SendCoreAsync(HttpMethod.Post, path, body, includeBearer: false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await CreateErrorMessageAsync(response));
        }

        return await ReadAsync<T>(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, bool allowRefresh = true)
    {
        var response = await SendCoreAsync(method, path, body, includeBearer: true);
        if (response.StatusCode == HttpStatusCode.Unauthorized && allowRefresh && await RefreshAsync())
        {
            response.Dispose();
            response = await SendCoreAsync(method, path, body, includeBearer: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await CreateErrorMessageAsync(response));
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, bool includeBearer)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (includeBearer && _tokens is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.AccessToken);
        }

        return await _httpClient.SendAsync(request);
    }

    private async Task<bool> RefreshAsync()
    {
        if (_tokens is null)
        {
            return false;
        }

        try
        {
            var result = await PostAnonymousAsync<AuthResult>("api/v1/auth/refresh", new { _tokens.RefreshToken });
            await SetTokensAsync(result.Tokens);
            return true;
        }
        catch
        {
            _tokens = null;
            _tokenStore.Clear();
            return false;
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException("API response was empty.");
    }

    private static async Task<string> CreateErrorMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body)
            ? $"API request failed: {(int)response.StatusCode} {response.ReasonPhrase}"
            : $"API request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}";
    }
}

public sealed record AuthResult(AuthenticatedUser User, TokenResponse Tokens);
public sealed record AuthenticatedUser(Guid Id, string Email, string DisplayName);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, DateTimeOffset RefreshTokenExpiresAtUtc);
public sealed record DeviceSummary(Guid Id, string Serial, string? DisplayName, string? Category, string Status, int ManifestVersion, DateTimeOffset? LastSeenAtUtc, DateTimeOffset? ClaimedAtUtc);
