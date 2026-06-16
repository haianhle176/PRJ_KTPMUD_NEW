using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using IoTPlatform.Wpf.Services;

namespace IoTPlatform.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IoTPlatformApiClient _api = new();
    private readonly DispatcherTimer _pollTimer;

    private string _currentSection = "Auth";
    private string _statusMessage = "Ready.";
    private string _email = "";
    private string _password = "";
    private string _displayName = "";
    private bool _isAuthenticated;
    private bool _isRegisterMode;
    private DeviceSummary? _selectedDevice;
    private AutomationRuleDto? _selectedAutomationRule;

    public MainViewModel()
    {
        LoginCommand = new RelayCommand(LoginAsync);
        RegisterCommand = new RelayCommand(RegisterAsync);
        SubmitAuthCommand = new RelayCommand(SubmitAuthAsync);
        ToggleAuthModeCommand = new RelayCommand(ToggleAuthModeAsync);
        LogoutCommand = new RelayCommand(LogoutAsync, () => IsAuthenticated);
        NavigateCommand = new RelayCommand<string>(NavigateAsync);
        RefreshDevicesCommand = new RelayCommand(RefreshDevicesAsync, () => IsAuthenticated);
        ClaimDeviceCommand = new RelayCommand(ClaimDeviceAsync, () => IsAuthenticated);
        RenameDeviceCommand = new RelayCommand(RenameDeviceAsync, () => IsAuthenticated && SelectedDevice is not null);
        LoadDeviceDetailCommand = new RelayCommand(LoadDeviceDetailAsync, () => IsAuthenticated && SelectedDevice is not null);
        SendCommandCommand = new RelayCommand(SendCommandAsync, () => IsAuthenticated && SelectedDevice is not null);
        SendActuatorCommand = new RelayCommand<ActuatorControl>(SendActuatorCommandAsync, control => IsAuthenticated && SelectedDevice is not null && control is not null);
        RefreshAutomationRulesCommand = new RelayCommand(RefreshAutomationRulesAsync, () => IsAuthenticated);
        RefreshNotificationsCommand = new RelayCommand(RefreshNotificationsAsync, () => IsAuthenticated);
        SaveAutomationRuleCommand = new RelayCommand(SaveAutomationRuleAsync, () => IsAuthenticated);
        DeleteAutomationRuleCommand = new RelayCommand(DeleteAutomationRuleAsync, () => IsAuthenticated && SelectedAutomationRule is not null);
        LoadAutomationExecutionsCommand = new RelayCommand(LoadAutomationExecutionsAsync, () => IsAuthenticated && SelectedAutomationRule is not null);
        LaunchWorkerCommand = new RelayCommand(() => LaunchLocalProjectAsync("src\\IoTPlatform.Worker", "Worker"));
        LaunchDeviceAppCommand = new RelayCommand(() => LaunchLocalProjectAsync("src\\IoTPlatform.DeviceApp", "DeviceApp"));

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _ = RestoreSessionAsync();
    }

    public ObservableCollection<DeviceSummary> Devices { get; } = [];
    public ObservableCollection<DeviceCapability> Capabilities { get; } = [];
    public ObservableCollection<SensorCard> Sensors { get; } = [];
    public ObservableCollection<ActuatorControl> Actuators { get; } = [];
    public ObservableCollection<TelemetryValue> LatestValues { get; } = [];
    public ObservableCollection<TelemetryHistoryItem> TelemetryHistory { get; } = [];
    public ObservableCollection<CommandDto> Commands { get; } = [];
    public ObservableCollection<AutomationRuleDto> AutomationRules { get; } = [];
    public ObservableCollection<AutomationExecutionDto> AutomationExecutions { get; } = [];
    public ObservableCollection<NotificationDto> Notifications { get; } = [];

    public RelayCommand LoginCommand { get; }
    public RelayCommand RegisterCommand { get; }
    public RelayCommand SubmitAuthCommand { get; }
    public RelayCommand ToggleAuthModeCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ClaimDeviceCommand { get; }
    public RelayCommand RenameDeviceCommand { get; }
    public RelayCommand LoadDeviceDetailCommand { get; }
    public RelayCommand SendCommandCommand { get; }
    public RelayCommand<ActuatorControl> SendActuatorCommand { get; }
    public RelayCommand RefreshAutomationRulesCommand { get; }
    public RelayCommand RefreshNotificationsCommand { get; }
    public RelayCommand SaveAutomationRuleCommand { get; }
    public RelayCommand DeleteAutomationRuleCommand { get; }
    public RelayCommand LoadAutomationExecutionsCommand { get; }
    public RelayCommand LaunchWorkerCommand { get; }
    public RelayCommand LaunchDeviceAppCommand { get; }

    public string CurrentSection
    {
        get => _currentSection;
        set
        {
            if (SetProperty(ref _currentSection, value))
            {
                OnPropertyChanged(nameof(AuthVisibility));
                OnPropertyChanged(nameof(HomeVisibility));
                OnPropertyChanged(nameof(DevicesVisibility));
                OnPropertyChanged(nameof(AutomationVisibility));
                OnPropertyChanged(nameof(NotificationsVisibility));
                OnPropertyChanged(nameof(AdvancedVisibility));
            }
        }
    }

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
                OnPropertyChanged(nameof(AuthenticatedVisibility));
                OnPropertyChanged(nameof(UnauthenticatedVisibility));
                OnPropertyChanged(nameof(AppWindowWidth));
                OnPropertyChanged(nameof(AppWindowHeight));
                OnPropertyChanged(nameof(AppWindowMinWidth));
                OnPropertyChanged(nameof(AppWindowMinHeight));
                OnPropertyChanged(nameof(AppWindowMaxWidth));
                OnPropertyChanged(nameof(AppWindowMaxHeight));
                OnPropertyChanged(nameof(AppResizeMode));
                RaiseCommandStates();
            }
        }
    }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            if (SetProperty(ref _isRegisterMode, value))
            {
                OnPropertyChanged(nameof(AuthTitle));
                OnPropertyChanged(nameof(AuthSubtitle));
                OnPropertyChanged(nameof(AuthPrimaryButtonText));
                OnPropertyChanged(nameof(AuthSwitchPrefix));
                OnPropertyChanged(nameof(AuthSwitchAction));
                OnPropertyChanged(nameof(RegisterFieldVisibility));
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
                OnPropertyChanged(nameof(SelectedDeviceName));
                OnPropertyChanged(nameof(SelectedDeviceStatus));
                OnPropertyChanged(nameof(HasSelectedDeviceVisibility));
                OnPropertyChanged(nameof(NoSelectedDeviceVisibility));
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
                    AutomationActiveFromTime = value.ActiveFromTime ?? "";
                    AutomationActiveToTime = value.ActiveToTime ?? "";
                    AutomationActiveDaysOfWeek = value.ActiveDaysOfWeek?.ToString() ?? "";
                    AutomationIsEnabled = value.IsEnabled;
                    OnAutomationPropertiesChanged();
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
    public string AutomationActiveFromTime { get; set; } = "";
    public string AutomationActiveToTime { get; set; } = "";
    public string AutomationActiveDaysOfWeek { get; set; } = "";
    public bool AutomationIsEnabled { get; set; } = true;

    public int OnlineDeviceCount => Devices.Count(device => device.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
    public int OfflineDeviceCount => Devices.Count(device => !device.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
    public int UnreadNotificationCount => Notifications.Count(notification => !notification.IsRead);
    public int EnabledAutomationCount => AutomationRules.Count(rule => rule.IsEnabled);
    public string SelectedDeviceName => SelectedDevice?.NameOrSerial ?? "Select a device";
    public string SelectedDeviceStatus => SelectedDevice is null ? "No device selected" : $"{SelectedDevice.Status} - Last seen {SelectedDevice.LastSeenAtUtc?.ToLocalTime().ToString("g") ?? "never"}";
    public string WorkerLaunchHelp => "OmniConnect runs from the cloud. These local shortcuts are only for your development machine when the source projects and dotnet SDK are available.";
    public string AuthTitle => IsRegisterMode ? "Tạo tài khoản" : "Đăng nhập";
    public string AuthSubtitle => IsRegisterMode
        ? "Bắt đầu quản lý các thiết bị IoT động của bạn."
        : "Chào mừng bạn quay lại OmniConnect.";
    public string AuthPrimaryButtonText => IsRegisterMode ? "Đăng ký" : "Đăng nhập";
    public string AuthSwitchPrefix => IsRegisterMode ? "Đã có tài khoản? " : "Chưa có tài khoản? ";
    public string AuthSwitchAction => IsRegisterMode ? "Đăng nhập" : "Đăng ký ngay";
    public double AppWindowWidth => IsAuthenticated ? 1280 : 420;
    public double AppWindowHeight => IsAuthenticated ? 820 : 520;
    public double AppWindowMinWidth => IsAuthenticated ? 1080 : 420;
    public double AppWindowMinHeight => IsAuthenticated ? 720 : 520;
    public double AppWindowMaxWidth => IsAuthenticated ? double.PositiveInfinity : 420;
    public double AppWindowMaxHeight => IsAuthenticated ? double.PositiveInfinity : 520;
    public ResizeMode AppResizeMode => IsAuthenticated ? ResizeMode.CanResize : ResizeMode.NoResize;

    public Visibility AuthVisibility => Section("Auth");
    public Visibility HomeVisibility => Section("Home");
    public Visibility DevicesVisibility => Section("Devices");
    public Visibility AutomationVisibility => Section("Automation");
    public Visibility NotificationsVisibility => Section("Notifications");
    public Visibility AdvancedVisibility => Section("Advanced");
    public Visibility AuthenticatedVisibility => IsAuthenticated ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UnauthenticatedVisibility => IsAuthenticated ? Visibility.Collapsed : Visibility.Visible;
    public Visibility HasSelectedDeviceVisibility => SelectedDevice is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NoSelectedDeviceVisibility => SelectedDevice is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyDevicesVisibility => Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasDevicesVisibility => Devices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyNotificationsVisibility => Notifications.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RegisterFieldVisibility => IsRegisterMode ? Visibility.Visible : Visibility.Collapsed;

    private Task ToggleAuthModeAsync()
    {
        IsRegisterMode = !IsRegisterMode;
        return Task.CompletedTask;
    }

    private Task SubmitAuthAsync() => IsRegisterMode ? RegisterAsync() : LoginAsync();

    private async Task NavigateAsync(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        CurrentSection = section;
        if (IsAuthenticated && section is "Home" or "Devices")
        {
            await RefreshDevicesAsync();
        }
        else if (IsAuthenticated && section == "Notifications")
        {
            await RefreshNotificationsAsync();
        }
    }

    private async Task RestoreSessionAsync()
    {
        if (await _api.TryRestoreAsync())
        {
            IsAuthenticated = true;
            CurrentSection = "Home";
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
            CurrentSection = "Home";
            _pollTimer.Start();
            await RefreshAllAfterAuthenticationAsync($"Signed in as {result.User.DisplayName}.");
        });
    }

    private async Task RegisterAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _api.RegisterAsync(Email, Password, DisplayName);
            IsAuthenticated = true;
            CurrentSection = "Home";
            _pollTimer.Start();
            await RefreshAllAfterAuthenticationAsync($"Created account for {result.User.DisplayName}.");
        });
    }

    private async Task RefreshAllAfterAuthenticationAsync(string successMessage)
    {
        try
        {
            await RefreshAllAsync();
            StatusMessage = successMessage;
        }
        catch (Exception exception)
        {
            StatusMessage = $"{successMessage} Data refresh failed: {exception.Message}";
        }
    }

    private async Task LogoutAsync()
    {
        await RunAsync(async () =>
        {
            await _api.LogoutAsync();
            IsAuthenticated = false;
            CurrentSection = "Auth";
            _pollTimer.Stop();
            Devices.Clear();
            Capabilities.Clear();
            Sensors.Clear();
            Actuators.Clear();
            LatestValues.Clear();
            TelemetryHistory.Clear();
            Commands.Clear();
            AutomationRules.Clear();
            AutomationExecutions.Clear();
            Notifications.Clear();
            NotifyDashboardCounts();
            StatusMessage = "Signed out.";
        });
    }

    private async Task RefreshAllAsync()
    {
        await RefreshDevicesAsync();
        await RefreshAutomationRulesAsync();
        await RefreshNotificationsAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        await RunAsync(async () =>
        {
            var previousSelectedId = SelectedDevice?.Id;
            Replace(Devices, await _api.GetDevicesAsync());
            SelectedDevice = Devices.FirstOrDefault(device => device.Id == previousSelectedId) ?? Devices.FirstOrDefault();
            NotifyDashboardCounts();
            StatusMessage = Devices.Count == 0
                ? "No devices yet. Start a DeviceApp and claim it with a pairing code."
                : $"Loaded {Devices.Count} devices.";
        });
    }

    private async Task ClaimDeviceAsync()
    {
        await RunAsync(async () =>
        {
            var device = await _api.ClaimDeviceAsync(ClaimPairingCode, ClaimDisplayName, EmptyToNull(ClaimCategory));
            await RefreshDevicesAsync();
            SelectedDevice = Devices.FirstOrDefault(item => item.Id == device.Id);
            CurrentSection = "Home";
            StatusMessage = $"Claimed {device.NameOrSerial}.";
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
            StatusMessage = "Device details saved.";
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
            var capabilities = await _api.GetDeviceCapabilitiesAsync(SelectedDevice.Id);
            Replace(Capabilities, capabilities);
            Replace(LatestValues, await _api.GetLatestValuesAsync(SelectedDevice.Id));
            var history = await _api.GetTelemetryHistoryAsync(SelectedDevice.Id, EmptyToNull(HistoryCapabilityKey));
            Replace(TelemetryHistory, history.Items);
            BuildControlModels();
            StatusMessage = $"Loaded {SelectedDevice.NameOrSerial}.";
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
            StatusMessage = $"Command {command.CommandName} is {command.Status}.";
        });
    }

    private async Task SendActuatorCommandAsync(ActuatorControl? control)
    {
        if (SelectedDevice is null || control is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var commandName = control.NextCommandName;
            var desiredValue = control.NextDesiredValue;
            var command = await _api.CreateCommandAsync(SelectedDevice.Id, control.Key, commandName, desiredValue);
            Commands.Insert(0, command);
            control.MarkSubmitted(commandName, command.Status);
            StatusMessage = $"Sent {commandName.ToLowerInvariant()} to {control.DisplayName}.";
        });
    }

    private async Task RefreshAutomationRulesAsync()
    {
        await RunAsync(async () =>
        {
            Replace(AutomationRules, await _api.GetAutomationRulesAsync());
            NotifyDashboardCounts();
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
                    AutomationIsEnabled,
                    EmptyToNull(AutomationActiveFromTime),
                    EmptyToNull(AutomationActiveToTime),
                    ParseNullableInt(AutomationActiveDaysOfWeek)));
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
                    AutomationIsEnabled,
                    EmptyToNull(AutomationActiveFromTime),
                    EmptyToNull(AutomationActiveToTime),
                    ParseNullableInt(AutomationActiveDaysOfWeek)));
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

    private async Task RefreshNotificationsAsync()
    {
        await RunAsync(async () =>
        {
            Replace(Notifications, await _api.GetNotificationsAsync());
            NotifyDashboardCounts();
            StatusMessage = Notifications.Count == 0 ? "No notifications." : $"Loaded {Notifications.Count} notifications.";
        });
    }

    private async Task LaunchLocalProjectAsync(string projectPath, string label)
    {
        await Task.CompletedTask;
        try
        {
            var root = FindSolutionRoot();
            if (root is null)
            {
                StatusMessage = $"{label} shortcut is only available from the OmniConnect source workspace.";
                return;
            }

            var fullProjectPath = Path.Combine(root, projectPath);
            if (!Directory.Exists(fullProjectPath))
            {
                StatusMessage = $"{label} project was not found on this machine.";
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\"",
                WorkingDirectory = root,
                UseShellExecute = true
            });
            StatusMessage = $"Opening {label}. If nothing appears, check that the .NET SDK is installed.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not open {label}: {exception.Message}";
        }
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
            await RefreshNotificationsAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void BuildControlModels()
    {
        var latestByCapability = LatestValues.ToDictionary(value => value.CapabilityKey, StringComparer.Ordinal);
        Replace(Sensors, Capabilities
            .Where(capability => capability.Kind == "sensor")
            .Select(capability =>
            {
                latestByCapability.TryGetValue(capability.Key, out var latest);
                return new SensorCard(
                    capability.Key,
                    capability.DisplayName,
                    capability.Unit,
                    latest?.DisplayValue ?? "No readings yet",
                    latest?.RecordedAtUtc.ToLocalTime().ToString("g") ?? "Waiting for telemetry");
            }));
        Replace(Actuators, Capabilities
            .Where(capability => capability.Kind == "actuator" && capability.IsWritable)
            .Select(capability =>
            {
                latestByCapability.TryGetValue(capability.Key, out var latest);
                var isOn = latest?.BooleanValue == true;
                return new ActuatorControl(
                    capability.Key,
                    capability.DisplayName,
                    capability.DataType,
                    capability.SupportedCommands,
                    isOn,
                    isOn ? "Currently on" : "Currently off");
            }));
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
        SendActuatorCommand.RaiseCanExecuteChanged();
        RefreshAutomationRulesCommand.RaiseCanExecuteChanged();
        RefreshNotificationsCommand.RaiseCanExecuteChanged();
        SaveAutomationRuleCommand.RaiseCanExecuteChanged();
        DeleteAutomationRuleCommand.RaiseCanExecuteChanged();
        LoadAutomationExecutionsCommand.RaiseCanExecuteChanged();
    }

    private void NotifyDashboardCounts()
    {
        OnPropertyChanged(nameof(OnlineDeviceCount));
        OnPropertyChanged(nameof(OfflineDeviceCount));
        OnPropertyChanged(nameof(UnreadNotificationCount));
        OnPropertyChanged(nameof(EnabledAutomationCount));
        OnPropertyChanged(nameof(EmptyDevicesVisibility));
        OnPropertyChanged(nameof(HasDevicesVisibility));
        OnPropertyChanged(nameof(EmptyNotificationsVisibility));
    }

    private void OnAutomationPropertiesChanged()
    {
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
        OnPropertyChanged(nameof(AutomationActiveFromTime));
        OnPropertyChanged(nameof(AutomationActiveToTime));
        OnPropertyChanged(nameof(AutomationActiveDaysOfWeek));
        OnPropertyChanged(nameof(AutomationIsEnabled));
    }

    private Visibility Section(string section) => CurrentSection == section ? Visibility.Visible : Visibility.Collapsed;

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

    private static int? ParseNullableInt(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : int.Parse(value.Trim());

    private static string? FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.GetFiles("IoTPlatform.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

public sealed record SensorCard(string Key, string DisplayName, string? Unit, string Value, string RecordedAt)
{
    public string ValueWithUnit => string.IsNullOrWhiteSpace(Unit) || Value == "No readings yet" ? Value : $"{Value} {Unit}";
}

public sealed class ActuatorControl(
    string key,
    string displayName,
    string dataType,
    IReadOnlyList<string> supportedCommands,
    bool isOn,
    string lastStatus) : ObservableObject
{
    private string _lastStatus = lastStatus;
    private bool _isOn = isOn;

    public string Key { get; } = key;
    public string DisplayName { get; } = displayName;
    public string DataType { get; } = dataType;
    public IReadOnlyList<string> SupportedCommands { get; } = supportedCommands;
    public bool IsOn
    {
        get => _isOn;
        private set
        {
            if (SetProperty(ref _isOn, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(ButtonText));
                OnPropertyChanged(nameof(NextCommandName));
                OnPropertyChanged(nameof(NextDesiredValue));
            }
        }
    }

    public string StateText => IsOn ? "On" : "Off";
    public string ButtonText => IsOn ? "Turn off" : "Turn on";
    public string NextCommandName => IsOn && SupportedCommands.Contains("OFF", StringComparer.OrdinalIgnoreCase) ? "OFF" : "ON";
    public JsonElement NextDesiredValue => DataType == "boolean"
        ? JsonSerializer.SerializeToElement(!IsOn)
        : JsonSerializer.SerializeToElement(IsOn ? "OFF" : "ON");

    public string LastStatus
    {
        get => _lastStatus;
        set => SetProperty(ref _lastStatus, value);
    }

    public void MarkSubmitted(string commandName, string status)
    {
        if (DataType == "boolean")
        {
            IsOn = !IsOn;
        }

        LastStatus = $"Command {commandName} is {status}. Waiting for device acknowledgement.";
    }
}
