using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Threading;
using IoTPlatform.Wpf.Services;

namespace IoTPlatform.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IoTPlatformApiClient _api = new();
    private readonly DispatcherTimer _pollTimer;

    private string _statusMessage = "Ready.";
    private string _email = "";
    private string _password = "";
    private string _displayName = "";
    private bool _isAuthenticated;
    private DeviceSummary? _selectedDevice;
    private AutomationRuleDto? _selectedAutomationRule;

    public MainViewModel()
    {
        LoginCommand = new RelayCommand(LoginAsync);
        RegisterCommand = new RelayCommand(RegisterAsync);
        LogoutCommand = new RelayCommand(LogoutAsync, () => IsAuthenticated);
        RefreshDevicesCommand = new RelayCommand(RefreshDevicesAsync, () => IsAuthenticated);
        ClaimDeviceCommand = new RelayCommand(ClaimDeviceAsync, () => IsAuthenticated);
        RenameDeviceCommand = new RelayCommand(RenameDeviceAsync, () => IsAuthenticated && SelectedDevice is not null);
        LoadDeviceDetailCommand = new RelayCommand(LoadDeviceDetailAsync, () => IsAuthenticated && SelectedDevice is not null);
        SendCommandCommand = new RelayCommand(SendCommandAsync, () => IsAuthenticated && SelectedDevice is not null);
        RefreshAutomationRulesCommand = new RelayCommand(RefreshAutomationRulesAsync, () => IsAuthenticated);
        SaveAutomationRuleCommand = new RelayCommand(SaveAutomationRuleAsync, () => IsAuthenticated);
        DeleteAutomationRuleCommand = new RelayCommand(DeleteAutomationRuleAsync, () => IsAuthenticated && SelectedAutomationRule is not null);
        LoadAutomationExecutionsCommand = new RelayCommand(LoadAutomationExecutionsAsync, () => IsAuthenticated && SelectedAutomationRule is not null);

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _ = RestoreSessionAsync();
    }

    public ObservableCollection<DeviceSummary> Devices { get; } = [];
    public ObservableCollection<TelemetryValue> LatestValues { get; } = [];
    public ObservableCollection<TelemetryHistoryItem> TelemetryHistory { get; } = [];
    public ObservableCollection<CommandDto> Commands { get; } = [];
    public ObservableCollection<AutomationRuleDto> AutomationRules { get; } = [];
    public ObservableCollection<AutomationExecutionDto> AutomationExecutions { get; } = [];

    public RelayCommand LoginCommand { get; }
    public RelayCommand RegisterCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ClaimDeviceCommand { get; }
    public RelayCommand RenameDeviceCommand { get; }
    public RelayCommand LoadDeviceDetailCommand { get; }
    public RelayCommand SendCommandCommand { get; }
    public RelayCommand RefreshAutomationRulesCommand { get; }
    public RelayCommand SaveAutomationRuleCommand { get; }
    public RelayCommand DeleteAutomationRuleCommand { get; }
    public RelayCommand LoadAutomationExecutionsCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            if (SetProperty(ref _isAuthenticated, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public DeviceSummary? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                RaiseCommandStates();
                if (value is not null)
                {
                    RenameDisplayName = value.DisplayName ?? "";
                    RenameCategory = value.Category ?? "";
                    AutomationSourceDeviceId = value.Id.ToString();
                    AutomationTargetDeviceId = value.Id.ToString();
                    OnPropertyChanged(nameof(RenameDisplayName));
                    OnPropertyChanged(nameof(RenameCategory));
                    OnPropertyChanged(nameof(AutomationSourceDeviceId));
                    OnPropertyChanged(nameof(AutomationTargetDeviceId));
                    _ = LoadDeviceDetailAsync();
                }
            }
        }
    }

    public AutomationRuleDto? SelectedAutomationRule
    {
        get => _selectedAutomationRule;
        set
        {
            if (SetProperty(ref _selectedAutomationRule, value))
            {
                RaiseCommandStates();
                if (value is not null)
                {
                    AutomationName = value.Name;
                    AutomationSourceDeviceId = value.SourceDeviceId.ToString();
                    AutomationSourceCapabilityKey = value.SourceCapabilityKey;
                    AutomationOperator = value.ComparisonOperator;
                    AutomationThresholdValue = FormatValue(value.ThresholdValueType, value.ThresholdNumberValue, value.ThresholdBooleanValue, value.ThresholdStringValue);
                    AutomationTargetDeviceId = value.TargetDeviceId.ToString();
                    AutomationTargetCapabilityKey = value.TargetCapabilityKey;
                    AutomationCommandName = value.CommandName;
                    AutomationDesiredValue = FormatValue(value.DesiredValueType, value.DesiredNumberValue, value.DesiredBooleanValue, value.DesiredStringValue);
                    AutomationCooldownSeconds = value.CooldownSeconds.ToString();
                    AutomationIsEnabled = value.IsEnabled;
                    OnPropertyChanged(nameof(AutomationName));
                    OnPropertyChanged(nameof(AutomationSourceDeviceId));
                    OnPropertyChanged(nameof(AutomationSourceCapabilityKey));
                    OnPropertyChanged(nameof(AutomationOperator));
                    OnPropertyChanged(nameof(AutomationThresholdValue));
                    OnPropertyChanged(nameof(AutomationTargetDeviceId));
                    OnPropertyChanged(nameof(AutomationTargetCapabilityKey));
                    OnPropertyChanged(nameof(AutomationCommandName));
                    OnPropertyChanged(nameof(AutomationDesiredValue));
                    OnPropertyChanged(nameof(AutomationCooldownSeconds));
                    OnPropertyChanged(nameof(AutomationIsEnabled));
                    _ = LoadAutomationExecutionsAsync();
                }
            }
        }
    }

    public string ClaimPairingCode { get; set; } = "";
    public string ClaimDisplayName { get; set; } = "";
    public string ClaimCategory { get; set; } = "";
    public string RenameDisplayName { get; set; } = "";
    public string RenameCategory { get; set; } = "";
    public string HistoryCapabilityKey { get; set; } = "";
    public string CommandCapabilityKey { get; set; } = "";
    public string CommandName { get; set; } = "ON";
    public string CommandDesiredValue { get; set; } = "true";

    public string AutomationName { get; set; } = "";
    public string AutomationSourceDeviceId { get; set; } = "";
    public string AutomationSourceCapabilityKey { get; set; } = "";
    public string AutomationOperator { get; set; } = ">";
    public string AutomationThresholdValue { get; set; } = "30";
    public string AutomationTargetDeviceId { get; set; } = "";
    public string AutomationTargetCapabilityKey { get; set; } = "";
    public string AutomationCommandName { get; set; } = "ON";
    public string AutomationDesiredValue { get; set; } = "true";
    public string AutomationCooldownSeconds { get; set; } = "30";
    public bool AutomationIsEnabled { get; set; } = true;

    private async Task RestoreSessionAsync()
    {
        if (await _api.TryRestoreAsync())
        {
            IsAuthenticated = true;
            StatusMessage = "Restored saved session.";
            _pollTimer.Start();
            await RefreshAllAsync();
        }
    }

    private async Task LoginAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _api.LoginAsync(Email, Password);
            IsAuthenticated = true;
            StatusMessage = $"Logged in as {result.User.DisplayName}.";
            _pollTimer.Start();
            await RefreshAllAsync();
        });
    }

    private async Task RegisterAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _api.RegisterAsync(Email, Password, DisplayName);
            IsAuthenticated = true;
            StatusMessage = $"Registered and logged in as {result.User.DisplayName}.";
            _pollTimer.Start();
            await RefreshAllAsync();
        });
    }

    private async Task LogoutAsync()
    {
        await RunAsync(async () =>
        {
            await _api.LogoutAsync();
            IsAuthenticated = false;
            _pollTimer.Stop();
            Devices.Clear();
            LatestValues.Clear();
            TelemetryHistory.Clear();
            Commands.Clear();
            AutomationRules.Clear();
            AutomationExecutions.Clear();
            StatusMessage = "Logged out.";
        });
    }

    private async Task RefreshAllAsync()
    {
        await RefreshDevicesAsync();
        await RefreshAutomationRulesAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        await RunAsync(async () =>
        {
            Replace(Devices, await _api.GetDevicesAsync());
            StatusMessage = $"Loaded {Devices.Count} devices.";
        });
    }

    private async Task ClaimDeviceAsync()
    {
        await RunAsync(async () =>
        {
            var device = await _api.ClaimDeviceAsync(ClaimPairingCode, ClaimDisplayName, EmptyToNull(ClaimCategory));
            await RefreshDevicesAsync();
            SelectedDevice = Devices.FirstOrDefault(item => item.Id == device.Id);
            StatusMessage = $"Claimed device {device.NameOrSerial}.";
        });
    }

    private async Task RenameDeviceAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var updated = await _api.UpdateDeviceAsync(SelectedDevice.Id, RenameDisplayName, EmptyToNull(RenameCategory));
            await RefreshDevicesAsync();
            SelectedDevice = Devices.FirstOrDefault(item => item.Id == updated.Id);
            StatusMessage = "Device updated.";
        });
    }

    private async Task LoadDeviceDetailAsync()
    {
        if (SelectedDevice is null || !IsAuthenticated)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Replace(LatestValues, await _api.GetLatestValuesAsync(SelectedDevice.Id));
            var history = await _api.GetTelemetryHistoryAsync(SelectedDevice.Id, EmptyToNull(HistoryCapabilityKey));
            Replace(TelemetryHistory, history.Items);
            StatusMessage = $"Loaded telemetry for {SelectedDevice.NameOrSerial}.";
        });
    }

    private async Task SendCommandAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var command = await _api.CreateCommandAsync(
                SelectedDevice.Id,
                CommandCapabilityKey,
                CommandName,
                ParseDynamicValue(CommandDesiredValue));
            Commands.Insert(0, command);
            StatusMessage = $"Command {command.CommandName} created: {command.Status}.";
        });
    }

    private async Task RefreshAutomationRulesAsync()
    {
        await RunAsync(async () =>
        {
            Replace(AutomationRules, await _api.GetAutomationRulesAsync());
            StatusMessage = $"Loaded {AutomationRules.Count} automation rules.";
        });
    }

    private async Task SaveAutomationRuleAsync()
    {
        await RunAsync(async () =>
        {
            var sourceDeviceId = Guid.Parse(AutomationSourceDeviceId);
            var targetDeviceId = Guid.Parse(AutomationTargetDeviceId);
            var cooldown = int.Parse(AutomationCooldownSeconds);
            if (SelectedAutomationRule is null)
            {
                var created = await _api.CreateAutomationRuleAsync(new CreateAutomationRulePayload(
                    AutomationName,
                    sourceDeviceId,
                    AutomationSourceCapabilityKey,
                    AutomationOperator,
                    ParseDynamicValue(AutomationThresholdValue),
                    targetDeviceId,
                    AutomationTargetCapabilityKey,
                    AutomationCommandName,
                    ParseDynamicValue(AutomationDesiredValue),
                    cooldown,
                    AutomationIsEnabled));
                await RefreshAutomationRulesAsync();
                SelectedAutomationRule = AutomationRules.FirstOrDefault(item => item.Id == created.Id);
                StatusMessage = "Automation rule created.";
            }
            else
            {
                var updated = await _api.UpdateAutomationRuleAsync(SelectedAutomationRule.Id, new UpdateAutomationRulePayload(
                    AutomationName,
                    AutomationOperator,
                    ParseDynamicValue(AutomationThresholdValue),
                    AutomationCommandName,
                    ParseDynamicValue(AutomationDesiredValue),
                    cooldown,
                    AutomationIsEnabled));
                await RefreshAutomationRulesAsync();
                SelectedAutomationRule = AutomationRules.FirstOrDefault(item => item.Id == updated.Id);
                StatusMessage = "Automation rule updated.";
            }
        });
    }

    private async Task DeleteAutomationRuleAsync()
    {
        if (SelectedAutomationRule is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _api.DeleteAutomationRuleAsync(SelectedAutomationRule.Id);
            SelectedAutomationRule = null;
            await RefreshAutomationRulesAsync();
            StatusMessage = "Automation rule deleted.";
        });
    }

    private async Task LoadAutomationExecutionsAsync()
    {
        if (SelectedAutomationRule is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Replace(AutomationExecutions, await _api.GetAutomationExecutionsAsync(SelectedAutomationRule.Id));
            StatusMessage = $"Loaded {AutomationExecutions.Count} automation executions.";
        });
    }

    private async Task PollAsync()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        try
        {
            await RefreshDevicesAsync();
            if (SelectedDevice is not null)
            {
                await LoadDeviceDetailAsync();
            }

            await RefreshAutomationRulesAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void RaiseCommandStates()
    {
        LoginCommand.RaiseCanExecuteChanged();
        RegisterCommand.RaiseCanExecuteChanged();
        LogoutCommand.RaiseCanExecuteChanged();
        RefreshDevicesCommand.RaiseCanExecuteChanged();
        ClaimDeviceCommand.RaiseCanExecuteChanged();
        RenameDeviceCommand.RaiseCanExecuteChanged();
        LoadDeviceDetailCommand.RaiseCanExecuteChanged();
        SendCommandCommand.RaiseCanExecuteChanged();
        RefreshAutomationRulesCommand.RaiseCanExecuteChanged();
        SaveAutomationRuleCommand.RaiseCanExecuteChanged();
        DeleteAutomationRuleCommand.RaiseCanExecuteChanged();
        LoadAutomationExecutionsCommand.RaiseCanExecuteChanged();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static JsonElement ParseDynamicValue(string value)
    {
        if (bool.TryParse(value, out var boolean))
        {
            return JsonSerializer.SerializeToElement(boolean);
        }

        if (decimal.TryParse(value, out var number))
        {
            return JsonSerializer.SerializeToElement(number);
        }

        return JsonSerializer.SerializeToElement(value);
    }

    private static string FormatValue(string valueType, decimal? numberValue, bool? booleanValue, string? stringValue) => valueType switch
    {
        "number" => numberValue?.ToString("0.###") ?? "",
        "boolean" => booleanValue?.ToString() ?? "",
        "string" => stringValue ?? "",
        _ => ""
    };

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
