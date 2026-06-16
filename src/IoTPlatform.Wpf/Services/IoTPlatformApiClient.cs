using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IoTPlatform.Wpf.Services;

public sealed class IoTPlatformApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStore _tokenStore = new();
    private AuthTokens? _tokens;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IoTPlatformApiClient()
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

    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName)
    {
        var result = await PostAnonymousAsync<AuthResult>("api/v1/auth/register", new { email, password, displayName });
        await SetTokensAsync(result.Tokens);
        return result;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var result = await PostAnonymousAsync<AuthResult>("api/v1/auth/login", new { email, password });
        await SetTokensAsync(result.Tokens);
        return result;
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

    public Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync() =>
        GetAsync<IReadOnlyList<DeviceSummary>>("api/v1/devices");

    public Task<IReadOnlyList<AvailableDevice>> GetAvailableDevicesAsync() =>
        GetAsync<IReadOnlyList<AvailableDevice>>("api/v1/devices/available");

    public Task<IReadOnlyList<DeviceCapability>> GetDeviceCapabilitiesAsync(Guid deviceId) =>
        GetAsync<IReadOnlyList<DeviceCapability>>($"api/v1/devices/{deviceId}/capabilities");

    public Task<DeviceSummary> ClaimDeviceAsync(string pairingCode, string displayName, string? category) =>
        PostAsync<DeviceSummary>("api/v1/devices/claim", new { pairingCode, displayName, category });

    public Task<DeviceSummary> UpdateDeviceAsync(Guid deviceId, string displayName, string? category) =>
        PatchAsync<DeviceSummary>($"api/v1/devices/{deviceId}", new { displayName, category });

    public Task<IReadOnlyList<TelemetryValue>> GetLatestValuesAsync(Guid deviceId) =>
        GetAsync<IReadOnlyList<TelemetryValue>>($"api/v1/devices/{deviceId}/latest-values");

    public Task<TelemetryHistoryPage> GetTelemetryHistoryAsync(Guid deviceId, string? capabilityKey = null)
    {
        var suffix = string.IsNullOrWhiteSpace(capabilityKey)
            ? string.Empty
            : $"?capabilityKey={Uri.EscapeDataString(capabilityKey)}";
        return GetAsync<TelemetryHistoryPage>($"api/v1/devices/{deviceId}/telemetry{suffix}");
    }

    public Task<CommandDto> CreateCommandAsync(Guid deviceId, string capabilityKey, string commandName, JsonElement desiredValue) =>
        PostAsync<CommandDto>($"api/v1/devices/{deviceId}/commands", new { capabilityKey, commandName, desiredValue });

    public Task<IReadOnlyList<AutomationRuleDto>> GetAutomationRulesAsync() =>
        GetAsync<IReadOnlyList<AutomationRuleDto>>("api/v1/automation-rules");

    public Task<AutomationRuleDto> CreateAutomationRuleAsync(CreateAutomationRulePayload payload) =>
        PostAsync<AutomationRuleDto>("api/v1/automation-rules", payload);

    public Task<AutomationRuleDto> UpdateAutomationRuleAsync(Guid id, UpdateAutomationRulePayload payload) =>
        PutAsync<AutomationRuleDto>($"api/v1/automation-rules/{id}", payload);

    public Task DeleteAutomationRuleAsync(Guid id) =>
        SendAsync(HttpMethod.Delete, $"api/v1/automation-rules/{id}");

    public Task<IReadOnlyList<AutomationExecutionDto>> GetAutomationExecutionsAsync(Guid id) =>
        GetAsync<IReadOnlyList<AutomationExecutionDto>>($"api/v1/automation-rules/{id}/executions");

    public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, int take = 50) =>
        GetAsync<IReadOnlyList<NotificationDto>>($"api/v1/notifications?unreadOnly={unreadOnly.ToString().ToLowerInvariant()}&take={take}");

    private async Task SetTokensAsync(TokenResponse tokens)
    {
        _tokens = new AuthTokens(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshTokenExpiresAtUtc);
        await _tokenStore.SaveAsync(_tokens);
    }

    private async Task<T> GetAsync<T>(string path) => await ReadAsync<T>(await SendAsync(HttpMethod.Get, path));

    private async Task<T> PostAsync<T>(string path, object body) => await ReadAsync<T>(await SendAsync(HttpMethod.Post, path, body));

    private async Task<T> PatchAsync<T>(string path, object body) => await ReadAsync<T>(await SendAsync(HttpMethod.Patch, path, body));

    private async Task<T> PutAsync<T>(string path, object body) => await ReadAsync<T>(await SendAsync(HttpMethod.Put, path, body));

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

public sealed record DeviceSummary(Guid Id, string Serial, string? DisplayName, string? Category, string Status, int ManifestVersion, DateTimeOffset? LastSeenAtUtc, DateTimeOffset? ClaimedAtUtc)
{
    public string NameOrSerial => string.IsNullOrWhiteSpace(DisplayName) ? Serial : DisplayName;
}

public sealed record AvailableDevice(
    Guid DeviceId,
    string Serial,
    string Status,
    int ManifestVersion,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset PairingCodeExpiresAtUtc,
    int SensorCount,
    int ActuatorCount);

public sealed record DeviceCapability(
    Guid Id,
    Guid DeviceId,
    string Key,
    string DisplayName,
    string Kind,
    string DataType,
    string? Unit,
    decimal? MinValue,
    decimal? MaxValue,
    bool IsWritable,
    IReadOnlyList<string> SupportedCommands);

public sealed record TelemetryValue(Guid CapabilityId, string CapabilityKey, string DisplayName, string ValueType, decimal? NumberValue, bool? BooleanValue, string? StringValue, DateTimeOffset RecordedAtUtc)
{
    public string DisplayValue => ValueType switch
    {
        "number" => NumberValue?.ToString("0.###") ?? "",
        "boolean" => BooleanValue?.ToString() ?? "",
        "string" => StringValue ?? "",
        _ => ""
    };
}

public sealed record TelemetryHistoryPage(IReadOnlyList<TelemetryHistoryItem> Items, int Page, int PageSize, int TotalCount);

public sealed record TelemetryHistoryItem(long Id, Guid CapabilityId, string CapabilityKey, string DisplayName, string ValueType, decimal? NumberValue, bool? BooleanValue, string? StringValue, DateTimeOffset RecordedAtUtc, DateTimeOffset ReceivedAtUtc)
{
    public string DisplayValue => ValueType switch
    {
        "number" => NumberValue?.ToString("0.###") ?? "",
        "boolean" => BooleanValue?.ToString() ?? "",
        "string" => StringValue ?? "",
        _ => ""
    };
}

public sealed record CommandDto(Guid Id, Guid DeviceId, string Serial, string CapabilityKey, string CommandName, string DesiredValueType, decimal? DesiredNumberValue, bool? DesiredBooleanValue, string? DesiredStringValue, string Source, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? SentAtUtc, DateTimeOffset? AcceptedAtUtc, DateTimeOffset? CompletedAtUtc, DateTimeOffset ExpiresAtUtc, string? FailureCode, string? FailureMessage);

public sealed record CreateAutomationRulePayload(string Name, Guid SourceDeviceId, string SourceCapabilityKey, string ComparisonOperator, JsonElement ThresholdValue, Guid TargetDeviceId, string TargetCapabilityKey, string CommandName, JsonElement DesiredValue, int CooldownSeconds, bool IsEnabled, string? ActiveFromTime, string? ActiveToTime, int? ActiveDaysOfWeek);

public sealed record UpdateAutomationRulePayload(string Name, string ComparisonOperator, JsonElement ThresholdValue, string CommandName, JsonElement DesiredValue, int CooldownSeconds, bool IsEnabled, string? ActiveFromTime, string? ActiveToTime, int? ActiveDaysOfWeek);

public sealed record AutomationRuleDto(Guid Id, string Name, Guid SourceDeviceId, string SourceDeviceSerial, string SourceCapabilityKey, string ComparisonOperator, string ThresholdValueType, decimal? ThresholdNumberValue, bool? ThresholdBooleanValue, string? ThresholdStringValue, Guid TargetDeviceId, string TargetDeviceSerial, string TargetCapabilityKey, string CommandName, string DesiredValueType, decimal? DesiredNumberValue, bool? DesiredBooleanValue, string? DesiredStringValue, bool IsEnabled, int CooldownSeconds, string? ActiveFromTime, string? ActiveToTime, int? ActiveDaysOfWeek, bool? LastConditionResult, DateTimeOffset? LastTriggeredAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record AutomationExecutionDto(long Id, Guid AutomationRuleId, Guid TriggerMessageId, Guid? CommandId, string Outcome, string? Reason, DateTimeOffset EvaluatedAtUtc);

public sealed record NotificationDto(long Id, Guid OwnerUserId, Guid? DeviceId, Guid? CapabilityId, string Type, string Severity, string Title, string Message, bool IsRead, DateTimeOffset CreatedAtUtc);
