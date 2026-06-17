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
    private bool _isSidebarExpanded = true;
    private bool _isNotificationFlyoutOpen;
    private bool _isRefreshingDeviceDetail;
    private string _lastHomeRefreshText = "Not refreshed yet";
    private string _availableDeviceMessage = "Waiting for DeviceApp...";
    private string _availablePairingCode = "";
    private string _availableClaimError = "";
    private string _homeDeviceFilter = "All";
    private string _homeMode = "Dashboard";
    private bool _isOnboardingClaimSuccess;
    private bool _isHomeDeviceRenaming;
    private bool _isAutomationScheduleEnabled;
    private bool _automationSunday;
    private bool _automationMonday;
    private bool _automationTuesday;
    private bool _automationWednesday;
    private bool _automationThursday;
    private bool _automationFriday;
    private bool _automationSaturday;
    private string _automationOperator = ">";
    private string _automationSourceSelectionKey = "";
    private string _automationTargetSelectionKey = "";
    private DeviceSummary? _selectedDevice;
    private HomeDeviceCard? _selectedHomeDeviceCard;
    private AvailableDevice? _selectedAvailableDevice;
    private AutomationRuleDto? _selectedAutomationRule;

    public MainViewModel()
    {
        LoginCommand = new RelayCommand(LoginAsync);
        RegisterCommand = new RelayCommand(RegisterAsync);
        SubmitAuthCommand = new RelayCommand(SubmitAuthAsync);
        ToggleAuthModeCommand = new RelayCommand(ToggleAuthModeAsync);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebarAsync, () => IsAuthenticated);
        LogoutCommand = new RelayCommand(LogoutAsync, () => IsAuthenticated);
        NavigateCommand = new RelayCommand<string>(NavigateAsync);
        RefreshHomeCommand = new RelayCommand(RefreshHomeAsync, () => IsAuthenticated);
        SetHomeDeviceFilterCommand = new RelayCommand<string>(SetHomeDeviceFilterAsync, _ => IsAuthenticated);
        OpenHomeDeviceCommand = new RelayCommand<HomeDeviceCard>(OpenHomeDeviceAsync, card => IsAuthenticated && card is not null);
        BackToHomeDashboardCommand = new RelayCommand(BackToHomeDashboardAsync, () => IsAuthenticated);
        OpenAddDeviceCommand = new RelayCommand(OpenAddDeviceAsync, () => IsAuthenticated);
        OpenCreateAutomationCommand = new RelayCommand(OpenCreateAutomationAsync, () => IsAuthenticated);
        StartHomeDeviceRenameCommand = new RelayCommand(StartHomeDeviceRenameAsync, () => IsAuthenticated && SelectedDevice is not null);
        CancelHomeDeviceRenameCommand = new RelayCommand(CancelHomeDeviceRenameAsync, () => IsAuthenticated && SelectedDevice is not null);
        ToggleHomeActuatorCommand = new RelayCommand<HomeDeviceCard>(ToggleHomeActuatorAsync, card => IsAuthenticated && card is not null && card.IsActuator && card.IsOnline);
        SelectDeviceCommand = new RelayCommand<DeviceSummary>(SelectDeviceAsync, device => IsAuthenticated && device is not null);
        SelectAvailableDeviceCommand = new RelayCommand<AvailableDevice>(SelectAvailableDeviceAsync, device => IsAuthenticated && device is not null);
        ClaimSelectedAvailableDeviceCommand = new RelayCommand(ClaimSelectedAvailableDeviceAsync, () => IsAuthenticated && SelectedAvailableDevice is not null);
        RefreshDevicesCommand = new RelayCommand(RefreshDevicesAsync, () => IsAuthenticated);
        ClaimDeviceCommand = new RelayCommand(ClaimDeviceAsync, () => IsAuthenticated);
        RenameDeviceCommand = new RelayCommand(RenameDeviceAsync, () => IsAuthenticated && SelectedDevice is not null);
        UnclaimDeviceCommand = new RelayCommand(UnclaimDeviceAsync, () => IsAuthenticated && SelectedDevice is not null);
        LoadDeviceDetailCommand = new RelayCommand(LoadDeviceDetailAsync, () => IsAuthenticated && SelectedDevice is not null);
        SendCommandCommand = new RelayCommand(SendCommandAsync, () => IsAuthenticated && SelectedDevice is not null);
        SendActuatorCommand = new RelayCommand<ActuatorControl>(SendActuatorCommandAsync, control => IsAuthenticated && SelectedDeviceIsOnline && control is not null && control.IsDeviceOnline);
        RefreshAutomationRulesCommand = new RelayCommand(RefreshAutomationRulesAsync, () => IsAuthenticated);
        RefreshNotificationsCommand = new RelayCommand(RefreshNotificationsAsync, () => IsAuthenticated);
        MarkAllNotificationsReadCommand = new RelayCommand(MarkAllNotificationsReadAsync, () => IsAuthenticated && UnreadNotificationCount > 0);
        ToggleNotificationFlyoutCommand = new RelayCommand(ToggleNotificationFlyoutAsync, () => IsAuthenticated);
        ViewAllNotificationsCommand = new RelayCommand(ViewAllNotificationsAsync, () => IsAuthenticated);
        SelectAutomationRuleCommand = new RelayCommand<AutomationRuleCard>(SelectAutomationRuleAsync, card => IsAuthenticated && card is not null);
        SaveAutomationRuleCommand = new RelayCommand(SaveAutomationRuleAsync, () => IsAuthenticated);
        DeleteAutomationRuleCommand = new RelayCommand(DeleteAutomationRuleAsync, () => IsAuthenticated && SelectedAutomationRule is not null);
        LoadAutomationExecutionsCommand = new RelayCommand(LoadAutomationExecutionsAsync, () => IsAuthenticated && SelectedAutomationRule is not null);
        LaunchDeviceAppCommand = new RelayCommand(() => LaunchLocalProjectAsync("src\\IoTPlatform.DeviceApp", "DeviceApp"));

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _ = RestoreSessionAsync();
    }

    public ObservableCollection<DeviceSummary> Devices { get; } = [];
    public ObservableCollection<AvailableDevice> AvailableDevices { get; } = [];
    public ObservableCollection<HomeDeviceCard> HomeDeviceCards { get; } = [];
    public ObservableCollection<DeviceCapability> Capabilities { get; } = [];
    public ObservableCollection<SensorCard> Sensors { get; } = [];
    public ObservableCollection<ActuatorControl> Actuators { get; } = [];
    public ObservableCollection<TelemetryValue> LatestValues { get; } = [];
    public ObservableCollection<TelemetryHistoryItem> TelemetryHistory { get; } = [];
    public ObservableCollection<HomeDeviceTelemetryLogItem> HomeDeviceTelemetryLog { get; } = [];
    public ObservableCollection<CommandDto> Commands { get; } = [];
    public ObservableCollection<AutomationRuleDto> AutomationRules { get; } = [];
    public ObservableCollection<AutomationRuleCard> AutomationRuleCards { get; } = [];
    public ObservableCollection<AutomationExecutionDto> AutomationExecutions { get; } = [];
    public ObservableCollection<NotificationDto> Notifications { get; } = [];

    public RelayCommand LoginCommand { get; }
    public RelayCommand RegisterCommand { get; }
    public RelayCommand SubmitAuthCommand { get; }
    public RelayCommand ToggleAuthModeCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand RefreshHomeCommand { get; }
    public RelayCommand<string> SetHomeDeviceFilterCommand { get; }
    public RelayCommand<HomeDeviceCard> OpenHomeDeviceCommand { get; }
    public RelayCommand BackToHomeDashboardCommand { get; }
    public RelayCommand OpenAddDeviceCommand { get; }
    public RelayCommand OpenCreateAutomationCommand { get; }
    public RelayCommand StartHomeDeviceRenameCommand { get; }
    public RelayCommand CancelHomeDeviceRenameCommand { get; }
    public RelayCommand<HomeDeviceCard> ToggleHomeActuatorCommand { get; }
    public RelayCommand<DeviceSummary> SelectDeviceCommand { get; }
    public RelayCommand<AvailableDevice> SelectAvailableDeviceCommand { get; }
    public RelayCommand ClaimSelectedAvailableDeviceCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ClaimDeviceCommand { get; }
    public RelayCommand RenameDeviceCommand { get; }
    public RelayCommand UnclaimDeviceCommand { get; }
    public RelayCommand LoadDeviceDetailCommand { get; }
    public RelayCommand SendCommandCommand { get; }
    public RelayCommand<ActuatorControl> SendActuatorCommand { get; }
    public RelayCommand RefreshAutomationRulesCommand { get; }
    public RelayCommand RefreshNotificationsCommand { get; }
    public RelayCommand MarkAllNotificationsReadCommand { get; }
    public RelayCommand ToggleNotificationFlyoutCommand { get; }
    public RelayCommand ViewAllNotificationsCommand { get; }
    public RelayCommand<AutomationRuleCard> SelectAutomationRuleCommand { get; }
    public RelayCommand SaveAutomationRuleCommand { get; }
    public RelayCommand DeleteAutomationRuleCommand { get; }
    public RelayCommand LoadAutomationExecutionsCommand { get; }
    public RelayCommand LaunchDeviceAppCommand { get; }

    public string CurrentSection
    {
        get => _currentSection;
        set
        {
            if (SetProperty(ref _currentSection, value))
            {
                if (value != "Home")
                {
                    IsNotificationFlyoutOpen = false;
                }

                OnPropertyChanged(nameof(AuthVisibility));
                OnPropertyChanged(nameof(HomeVisibility));
                OnPropertyChanged(nameof(DevicesVisibility));
                OnPropertyChanged(nameof(AutomationVisibility));
                OnPropertyChanged(nameof(NotificationsVisibility));
                OnPropertyChanged(nameof(AdvancedVisibility));
                OnPropertyChanged(nameof(SettingsVisibility));
                OnPropertyChanged(nameof(HomeNotificationFlyoutVisibility));
                OnPropertyChanged(nameof(IsHomeNavSelected));
                OnPropertyChanged(nameof(IsAutomationNavSelected));
                OnPropertyChanged(nameof(IsNotificationsNavSelected));
                OnPropertyChanged(nameof(IsAdvancedNavSelected));
                OnPropertyChanged(nameof(IsSettingsNavSelected));
                OnPropertyChanged(nameof(HomeNavBackground));
                OnPropertyChanged(nameof(HomeNavForeground));
                OnPropertyChanged(nameof(HomeNavRailOpacity));
                OnPropertyChanged(nameof(AutomationNavBackground));
                OnPropertyChanged(nameof(AutomationNavForeground));
                OnPropertyChanged(nameof(AutomationNavRailOpacity));
                OnPropertyChanged(nameof(NotificationsNavBackground));
                OnPropertyChanged(nameof(NotificationsNavForeground));
                OnPropertyChanged(nameof(NotificationsNavRailOpacity));
                OnPropertyChanged(nameof(AdvancedNavBackground));
                OnPropertyChanged(nameof(AdvancedNavForeground));
                OnPropertyChanged(nameof(AdvancedNavRailOpacity));
                OnPropertyChanged(nameof(SettingsNavBackground));
                OnPropertyChanged(nameof(SettingsNavForeground));
                OnPropertyChanged(nameof(SettingsNavRailOpacity));
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

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
                OnPropertyChanged(nameof(SidebarTextVisibility));
                OnPropertyChanged(nameof(SidebarExpandedVisibility));
                OnPropertyChanged(nameof(SidebarCollapsedVisibility));
                OnPropertyChanged(nameof(SidebarUnreadNotificationBadgeVisibility));
                OnPropertyChanged(nameof(SidebarToggleIcon));
                OnPropertyChanged(nameof(SidebarNavMargin));
                OnPropertyChanged(nameof(SidebarHeaderMargin));
                OnPropertyChanged(nameof(SidebarFooterMargin));
                OnPropertyChanged(nameof(SidebarNavPadding));
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
                NotifySelectedDeviceChanged();
                if (value is not null)
                {
                    RenameDisplayName = value.DisplayName ?? "";
                    RenameCategory = value.Category ?? "";
                    AutomationSourceDeviceId = value.Id.ToString();
                    AutomationTargetDeviceId = value.Id.ToString();
                    AutomationSourceSelectionKey = CreateAutomationSelectionKey(value.Id, AutomationSourceCapabilityKey);
                    AutomationTargetSelectionKey = CreateAutomationSelectionKey(value.Id, AutomationTargetCapabilityKey);
                    OnPropertyChanged(nameof(RenameDisplayName));
                    OnPropertyChanged(nameof(RenameCategory));
                    OnPropertyChanged(nameof(AutomationSourceDeviceId));
                    OnPropertyChanged(nameof(AutomationTargetDeviceId));
                    _ = LoadDeviceDetailAsync();
                }
                else
                {
                    SelectedHomeDeviceCard = null;
                    IsHomeDeviceRenaming = false;
                    Capabilities.Clear();
                    Sensors.Clear();
                    Actuators.Clear();
                    LatestValues.Clear();
                    TelemetryHistory.Clear();
                    HomeDeviceTelemetryLog.Clear();
                    NotifyDashboardCounts();
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
                    AutomationSourceDeviceId = value.SourceDeviceId?.ToString() ?? "";
                    AutomationSourceCapabilityKey = value.SourceCapabilityKey ?? "";
                    AutomationSourceSelectionKey = value.SourceDeviceId is null || string.IsNullOrWhiteSpace(value.SourceCapabilityKey)
                        ? ""
                        : CreateAutomationSelectionKey(value.SourceDeviceId.Value, value.SourceCapabilityKey);
                    AutomationOperator = value.ComparisonOperator ?? "";
                    AutomationThresholdValue = FormatValue(value.ThresholdValueType, value.ThresholdNumberValue, value.ThresholdBooleanValue, value.ThresholdStringValue);
                    AutomationTargetDeviceId = value.TargetDeviceId.ToString();
                    AutomationTargetCapabilityKey = value.TargetCapabilityKey;
                    AutomationTargetSelectionKey = CreateAutomationSelectionKey(value.TargetDeviceId, value.TargetCapabilityKey);
                    AutomationCommandName = value.CommandName;
                    AutomationDesiredValue = FormatValue(value.DesiredValueType, value.DesiredNumberValue, value.DesiredBooleanValue, value.DesiredStringValue);
                    AutomationCooldownSeconds = value.CooldownSeconds.ToString();
                    SetAutomationScheduleFromRule(value);
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
    public string AutomationOperator
    {
        get => _automationOperator;
        set
        {
            if (SetProperty(ref _automationOperator, value))
            {
                OnPropertyChanged(nameof(IsAutomationConditionEnabled));
            }
        }
    }
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

    public bool IsAutomationScheduleEnabled
    {
        get => _isAutomationScheduleEnabled;
        set
        {
            if (SetProperty(ref _isAutomationScheduleEnabled, value))
            {
                OnPropertyChanged(nameof(AutomationScheduleVisibility));
            }
        }
    }

    public bool AutomationSunday
    {
        get => _automationSunday;
        set => SetScheduleDay(ref _automationSunday, value, nameof(AutomationSunday));
    }

    public bool AutomationMonday
    {
        get => _automationMonday;
        set => SetScheduleDay(ref _automationMonday, value, nameof(AutomationMonday));
    }

    public bool AutomationTuesday
    {
        get => _automationTuesday;
        set => SetScheduleDay(ref _automationTuesday, value, nameof(AutomationTuesday));
    }

    public bool AutomationWednesday
    {
        get => _automationWednesday;
        set => SetScheduleDay(ref _automationWednesday, value, nameof(AutomationWednesday));
    }

    public bool AutomationThursday
    {
        get => _automationThursday;
        set => SetScheduleDay(ref _automationThursday, value, nameof(AutomationThursday));
    }

    public bool AutomationFriday
    {
        get => _automationFriday;
        set => SetScheduleDay(ref _automationFriday, value, nameof(AutomationFriday));
    }

    public bool AutomationSaturday
    {
        get => _automationSaturday;
        set => SetScheduleDay(ref _automationSaturday, value, nameof(AutomationSaturday));
    }

    public bool AutomationAllDays
    {
        get => AutomationSunday && AutomationMonday && AutomationTuesday && AutomationWednesday && AutomationThursday && AutomationFriday && AutomationSaturday;
        set
        {
            SetScheduleDayValues(value ? 127 : 0);
            AutomationActiveDaysOfWeek = GetLocalScheduleDayMask().ToString();
            OnAutomationPropertiesChanged();
        }
    }

    public string AutomationSourceSelectionKey
    {
        get => _automationSourceSelectionKey;
        set
        {
            if (SetProperty(ref _automationSourceSelectionKey, value))
            {
                ApplyAutomationSelection(value, isSource: true);
                OnPropertyChanged(nameof(IsAutomationTriggerConfigured));
                OnPropertyChanged(nameof(IsAutomationConditionEnabled));
            }
        }
    }

    public string AutomationTargetSelectionKey
    {
        get => _automationTargetSelectionKey;
        set
        {
            if (SetProperty(ref _automationTargetSelectionKey, value))
            {
                ApplyAutomationSelection(value, isSource: false);
            }
        }
    }

    public AvailableDevice? SelectedAvailableDevice
    {
        get => _selectedAvailableDevice;
        set
        {
            if (SetProperty(ref _selectedAvailableDevice, value))
            {
                AvailableClaimError = "";
                OnPropertyChanged(nameof(SelectedAvailableDeviceName));
                OnPropertyChanged(nameof(SelectedAvailableDeviceVisibility));
                ClaimSelectedAvailableDeviceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AvailablePairingCode
    {
        get => _availablePairingCode;
        set
        {
            if (SetProperty(ref _availablePairingCode, value))
            {
                AvailableClaimError = "";
            }
        }
    }

    public string AvailableClaimError
    {
        get => _availableClaimError;
        set
        {
            if (SetProperty(ref _availableClaimError, value))
            {
                OnPropertyChanged(nameof(AvailableClaimErrorVisibility));
            }
        }
    }

    public bool IsOnboardingClaimSuccess
    {
        get => _isOnboardingClaimSuccess;
        set
        {
            if (SetProperty(ref _isOnboardingClaimSuccess, value))
            {
                NotifyDashboardCounts();
            }
        }
    }

    public int OnlineDeviceCount => Devices.Count(device => device.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
    public int OfflineDeviceCount => Devices.Count(device => !device.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
    public int UnreadNotificationCount => Notifications.Count(notification => !notification.IsRead);
    public int SelectedSensorCount => Sensors.Count;
    public int SelectedActuatorCount => Actuators.Count;
    public int HomeSensorCount => HomeDeviceCards.Count(card => card.IsSensor);
    public int HomeActuatorCount => HomeDeviceCards.Count(card => card.IsActuator);
    public int EnabledAutomationCount => AutomationRules.Count(rule => rule.IsEnabled);
    public string UnreadNotificationBadgeText => UnreadNotificationCount > 99 ? "99+" : UnreadNotificationCount.ToString();
    public string HomeDeviceFilter
    {
        get => _homeDeviceFilter;
        set
        {
            if (SetProperty(ref _homeDeviceFilter, value))
            {
                OnPropertyChanged(nameof(FilteredHomeDeviceCards));
                OnPropertyChanged(nameof(HomeViewAllDevicesVisibility));
                OnPropertyChanged(nameof(AllFilterBackground));
                OnPropertyChanged(nameof(OnlineFilterBackground));
                OnPropertyChanged(nameof(OfflineFilterBackground));
                OnPropertyChanged(nameof(ActuatorsFilterBackground));
                OnPropertyChanged(nameof(SensorsFilterBackground));
                OnPropertyChanged(nameof(AllFilterForeground));
                OnPropertyChanged(nameof(OnlineFilterForeground));
                OnPropertyChanged(nameof(OfflineFilterForeground));
                OnPropertyChanged(nameof(ActuatorsFilterForeground));
                OnPropertyChanged(nameof(SensorsFilterForeground));
            }
        }
    }

    public string HomeMode
    {
        get => _homeMode;
        set
        {
            if (SetProperty(ref _homeMode, value))
            {
                OnPropertyChanged(nameof(HomeEmptyVisibility));
                OnPropertyChanged(nameof(HomeDashboardVisibility));
                OnPropertyChanged(nameof(HomeAddDeviceVisibility));
                OnPropertyChanged(nameof(HomeDeviceDetailVisibility));
                OnPropertyChanged(nameof(HomeNotificationFlyoutVisibility));
            }
        }
    }

    public bool IsHomeDeviceRenaming
    {
        get => _isHomeDeviceRenaming;
        set
        {
            if (SetProperty(ref _isHomeDeviceRenaming, value))
            {
                OnPropertyChanged(nameof(HomeDeviceNameTextVisibility));
                OnPropertyChanged(nameof(HomeDeviceRenameEditorVisibility));
            }
        }
    }

    public HomeDeviceCard? SelectedHomeDeviceCard
    {
        get => _selectedHomeDeviceCard;
        set
        {
            if (SetProperty(ref _selectedHomeDeviceCard, value))
            {
                OnPropertyChanged(nameof(SelectedDeviceTypeLabel));
                OnPropertyChanged(nameof(SelectedDeviceIconData));
            }
        }
    }

    public IEnumerable<HomeDeviceCard> FilteredHomeDeviceCards => HomeDeviceFilter switch
    {
        "Online" => HomeDeviceCards.Where(card => card.IsOnline),
        "Offline" => HomeDeviceCards.Where(card => !card.IsOnline),
        "Actuators" => HomeDeviceCards.Where(card => card.IsActuator),
        "Sensors" => HomeDeviceCards.Where(card => card.IsSensor),
        _ => HomeDeviceCards
    };
    public Visibility HomeViewAllDevicesVisibility => FilteredHomeDeviceCards.Count() > 6 ? Visibility.Visible : Visibility.Collapsed;
    public string AllFilterBackground => FilterBackground("All");
    public string OnlineFilterBackground => FilterBackground("Online");
    public string OfflineFilterBackground => FilterBackground("Offline");
    public string ActuatorsFilterBackground => FilterBackground("Actuators");
    public string SensorsFilterBackground => FilterBackground("Sensors");
    public string AllFilterForeground => FilterForeground("All");
    public string OnlineFilterForeground => FilterForeground("Online");
    public string OfflineFilterForeground => FilterForeground("Offline");
    public string ActuatorsFilterForeground => FilterForeground("Actuators");
    public string SensorsFilterForeground => FilterForeground("Sensors");
    public bool IsHomeNavSelected => CurrentSection == "Home";
    public bool IsAutomationNavSelected => CurrentSection == "Automation";
    public bool IsNotificationsNavSelected => CurrentSection == "Notifications";
    public bool IsAdvancedNavSelected => CurrentSection == "Advanced";
    public bool IsSettingsNavSelected => CurrentSection == "Settings";
    public string HomeNavBackground => NavBackground(IsHomeNavSelected);
    public string HomeNavForeground => NavForeground(IsHomeNavSelected);
    public double HomeNavRailOpacity => NavRailOpacity(IsHomeNavSelected);
    public string AutomationNavBackground => NavBackground(IsAutomationNavSelected);
    public string AutomationNavForeground => NavForeground(IsAutomationNavSelected);
    public double AutomationNavRailOpacity => NavRailOpacity(IsAutomationNavSelected);
    public string NotificationsNavBackground => NavBackground(IsNotificationsNavSelected);
    public string NotificationsNavForeground => NavForeground(IsNotificationsNavSelected);
    public double NotificationsNavRailOpacity => NavRailOpacity(IsNotificationsNavSelected);
    public string AdvancedNavBackground => NavBackground(IsAdvancedNavSelected);
    public string AdvancedNavForeground => NavForeground(IsAdvancedNavSelected);
    public double AdvancedNavRailOpacity => NavRailOpacity(IsAdvancedNavSelected);
    public string SettingsNavBackground => NavBackground(IsSettingsNavSelected);
    public string SettingsNavForeground => NavForeground(IsSettingsNavSelected);
    public double SettingsNavRailOpacity => NavRailOpacity(IsSettingsNavSelected);
    public IEnumerable<AutomationCapabilityOption> AutomationSourceOptions => HomeDeviceCards
        .Where(card => card.IsSensor)
        .Select(AutomationCapabilityOption.From)
        .Prepend(AutomationCapabilityOption.Empty);
    public IEnumerable<AutomationCapabilityOption> AutomationTargetOptions => HomeDeviceCards
        .Where(card => card.IsActuator)
        .Select(AutomationCapabilityOption.From);
    public IReadOnlyList<string> AutomationOperatorOptions { get; } = [">", ">=", "<", "<=", "==", "!="];
    public IReadOnlyList<string> AutomationTimeOptions { get; } = BuildTimeOptions();
    public bool IsAutomationTriggerConfigured => !string.IsNullOrWhiteSpace(AutomationSourceSelectionKey);
    public bool IsAutomationConditionEnabled => IsAutomationTriggerConfigured && !string.IsNullOrWhiteSpace(AutomationOperator);
    public string LastHomeRefreshText
    {
        get => _lastHomeRefreshText;
        set => SetProperty(ref _lastHomeRefreshText, value);
    }

    public string AvailableDeviceMessage
    {
        get => _availableDeviceMessage;
        set => SetProperty(ref _availableDeviceMessage, value);
    }

    public bool IsNotificationFlyoutOpen
    {
        get => _isNotificationFlyoutOpen;
        set
        {
            if (SetProperty(ref _isNotificationFlyoutOpen, value))
            {
                OnPropertyChanged(nameof(NotificationFlyoutVisibility));
                OnPropertyChanged(nameof(HomeNotificationFlyoutVisibility));
            }
        }
    }

    public IEnumerable<NotificationDto> RecentHomeNotifications => Notifications.Take(5);
    public IEnumerable<NotificationDto> RecentActivityNotifications => Notifications.Take(20);
    public string OnboardingTitle => IsOnboardingClaimSuccess ? "You are all set!" :
        AvailableDevices.Count == 0 ? "Start DeviceApp to connect your first device." : "Device found";
    public string OnboardingSubtitle => IsOnboardingClaimSuccess
        ? "Your device has been added and is ready to use."
        : AvailableDevices.Count == 0
        ? "DeviceApp runs locally and publishes your simulated SmartHome devices to OmniConnect."
        : "Enter the pairing code shown in DeviceApp to claim this device.";
    public string OnboardingHeroIcon => IsOnboardingClaimSuccess ? "\uE73E" : "\uE8B7";
    public string OnboardingHeroIconForeground => IsOnboardingClaimSuccess ? "#16A34A" : "#2563EB";
    public string OnboardingHeroIconBackground => IsOnboardingClaimSuccess ? "#DCFCE7" : "#EFF6FF";
    public bool IsOnboardingDeviceDetected => AvailableDevices.Count > 0;
    public string Step2DotText => IsOnboardingDeviceDetected || IsOnboardingClaimSuccess ? "\uE73E" : "2";
    public string Step3DotText => IsOnboardingClaimSuccess ? "\uE73E" : "3";
    public string Step4DotText => IsOnboardingClaimSuccess ? "\uE73E" : "4";
    public string Step2Background => IsOnboardingDeviceDetected || IsOnboardingClaimSuccess ? "#DCFCE7" : "#2563EB";
    public string Step2Border => IsOnboardingDeviceDetected || IsOnboardingClaimSuccess ? "#22C55E" : "#2563EB";
    public string Step2Foreground => IsOnboardingDeviceDetected || IsOnboardingClaimSuccess ? "#16A34A" : "#FFFFFF";
    public string Step3Background => IsOnboardingClaimSuccess ? "#DCFCE7" : IsOnboardingDeviceDetected ? "#2563EB" : "#FFFFFF";
    public string Step3Border => IsOnboardingClaimSuccess ? "#22C55E" : IsOnboardingDeviceDetected ? "#2563EB" : "#D8E0EA";
    public string Step3Foreground => IsOnboardingClaimSuccess ? "#16A34A" : IsOnboardingDeviceDetected ? "#FFFFFF" : "#64748B";
    public string Step4Background => IsOnboardingClaimSuccess ? "#2563EB" : "#FFFFFF";
    public string Step4Border => IsOnboardingClaimSuccess ? "#2563EB" : "#D8E0EA";
    public string Step4Foreground => IsOnboardingClaimSuccess ? "#FFFFFF" : "#64748B";
    public string Step2LabelForeground => "#2563EB";
    public string Step3LabelForeground => IsOnboardingDeviceDetected || IsOnboardingClaimSuccess ? "#2563EB" : "#64748B";
    public string Step4LabelForeground => IsOnboardingClaimSuccess ? "#2563EB" : "#64748B";
    public string Step2ConnectorBrush => IsOnboardingDeviceDetected || IsOnboardingClaimSuccess ? "#2563EB" : "#D8E0EA";
    public string Step3ConnectorBrush => IsOnboardingClaimSuccess ? "#2563EB" : "#D8E0EA";
    public string SelectedAvailableDeviceName => SelectedAvailableDevice?.Serial ?? "selected device";
    public string SelectedDeviceName => SelectedDevice?.NameOrSerial ?? "Select a device";
    public string SelectedDeviceStatus => SelectedDevice is null ? "No device selected" : $"{SelectedDevice.Status} - Last seen {SelectedDevice.LastSeenAtUtc?.ToLocalTime().ToString("g") ?? "never"}";
    public string SelectedDeviceTitle => SelectedDevice?.NameOrSerial ?? "Device";
    public string SelectedDeviceSubtitle => SelectedDevice is null
        ? "No device selected"
        : $"{SelectedDevice.Status} - Last seen {SelectedDevice.LastSeenAtUtc?.ToLocalTime().ToString("g") ?? "never"}";
    public string SelectedDeviceStatusBrush => SelectedDevice?.Status.Equals("online", StringComparison.OrdinalIgnoreCase) == true ? "#16A34A" : "#64748B";
    public string SelectedDeviceStatusBackground => SelectedDeviceIsOnline ? "#DCFCE7" : "#F1F5F9";
    public bool SelectedDeviceIsOnline => SelectedDevice?.Status.Equals("online", StringComparison.OrdinalIgnoreCase) == true;
    public string SelectedDeviceContentBrush => SelectedDeviceIsOnline ? "#0F172A" : "#94A3B8";
    public string SelectedDeviceMutedBrush => SelectedDeviceIsOnline ? "#64748B" : "#94A3B8";
    public string SelectedDeviceSurfaceBrush => SelectedDeviceIsOnline ? "#EFF6FF" : "#E2E8F0";
    public string SelectedDeviceIconBrush => SelectedDeviceIsOnline ? "#2563EB" : "#94A3B8";
    public string SelectedDeviceTypeLabel => ResolveSelectedDeviceTypeLabel();
    public string SelectedDeviceIconData => SelectedHomeDeviceCard?.KindIconData ?? ResolveDeviceIconData(ResolveSelectedDeviceSeed());
    public int SelectedDeviceLatestCount => LatestValues.Count;
    public string LocalToolsHelp => "OmniConnect runs from the cloud. Open DeviceApp locally when you want to simulate devices from this machine.";
    public string AuthTitle => IsRegisterMode ? "Tạo tài khoản" : "Đăng nhập";
    public string AuthSubtitle => IsRegisterMode
        ? "Bắt đầu quản lý các thiết bị IoT động của bạn."
        : "Chào mừng bạn quay lại OmniConnect.";
    public string AuthPrimaryButtonText => IsRegisterMode ? "Đăng ký" : "Đăng nhập";
    public string AuthSwitchPrefix => IsRegisterMode ? "Đã có tài khoản? " : "Chưa có tài khoản? ";
    public string AuthSwitchAction => IsRegisterMode ? "Đăng nhập" : "Đăng ký ngay";
    public double AppWindowWidth => IsAuthenticated ? 1180 : 420;
    public double AppWindowHeight => IsAuthenticated ? 700 : 520;
    public double AppWindowMinWidth => IsAuthenticated ? 1180 : 420;
    public double AppWindowMinHeight => IsAuthenticated ? 700 : 520;
    public double AppWindowMaxWidth => IsAuthenticated ? 1180 : 420;
    public double AppWindowMaxHeight => IsAuthenticated ? 700 : 520;
    public ResizeMode AppResizeMode => ResizeMode.NoResize;
    public GridLength SidebarWidth => new(IsSidebarExpanded ? 216 : 72);
    public Thickness SidebarHeaderMargin => IsSidebarExpanded ? new Thickness(18, 22, 16, 0) : new Thickness(14, 22, 14, 0);
    public Thickness SidebarNavMargin => IsSidebarExpanded ? new Thickness(8, 0, 12, 0) : new Thickness(8, 0, 8, 0);
    public Thickness SidebarFooterMargin => IsSidebarExpanded ? new Thickness(18, 0, 18, 20) : new Thickness(14, 0, 14, 20);
    public Thickness SidebarNavPadding => IsSidebarExpanded ? new Thickness(18, 0, 12, 0) : new Thickness(18, 0, 0, 0);
    public Visibility SidebarTextVisibility => IsSidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SidebarExpandedVisibility => IsSidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SidebarCollapsedVisibility => IsSidebarExpanded ? Visibility.Collapsed : Visibility.Visible;
    public string SidebarToggleIcon => IsSidebarExpanded ? "\uE76B" : "\uE76C";

    public Visibility AuthVisibility => Section("Auth");
    public Visibility HomeVisibility => Section("Home");
    public Visibility DevicesVisibility => Section("Devices");
    public Visibility AutomationVisibility => Section("Automation");
    public Visibility NotificationsVisibility => Section("Notifications");
    public Visibility AdvancedVisibility => Section("Advanced");
    public Visibility SettingsVisibility => Section("Settings");
    public Visibility AuthenticatedVisibility => IsAuthenticated ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UnauthenticatedVisibility => IsAuthenticated ? Visibility.Collapsed : Visibility.Visible;
    public Visibility HasSelectedDeviceVisibility => SelectedDevice is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NoSelectedDeviceVisibility => SelectedDevice is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyDevicesVisibility => Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasDevicesVisibility => Devices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HomeEmptyVisibility => HomeAddDeviceVisibility;
    public Visibility HomeDashboardVisibility => HomeMode == "Dashboard" && Devices.Count > 0 && !IsOnboardingClaimSuccess ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HomeAddDeviceVisibility => HomeMode == "AddDevice" || Devices.Count == 0 || IsOnboardingClaimSuccess ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HomeDeviceDetailVisibility => HomeMode == "DeviceDetail" && SelectedDevice is not null && !IsOnboardingClaimSuccess ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HomeDeviceNameTextVisibility => IsHomeDeviceRenaming ? Visibility.Collapsed : Visibility.Visible;
    public Visibility HomeDeviceRenameEditorVisibility => IsHomeDeviceRenaming ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyAvailableDevicesVisibility => AvailableDevices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasAvailableDevicesVisibility => AvailableDevices.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OnboardingWaitingVisibility => AvailableDevices.Count == 0 && !IsOnboardingClaimSuccess ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OnboardingClaimVisibility => AvailableDevices.Count > 0 && !IsOnboardingClaimSuccess ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OnboardingSuccessVisibility => IsOnboardingClaimSuccess ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectedAvailableDeviceVisibility => SelectedAvailableDevice is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AvailableClaimErrorVisibility => string.IsNullOrWhiteSpace(AvailableClaimError) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EmptyNotificationsVisibility => Notifications.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasNotificationsVisibility => Notifications.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyHomeTelemetryLogVisibility => HomeDeviceTelemetryLog.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasHomeTelemetryLogVisibility => HomeDeviceTelemetryLog.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AutomationScheduleVisibility => IsAutomationScheduleEnabled ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NotificationFlyoutVisibility => IsNotificationFlyoutOpen ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HomeNotificationFlyoutVisibility => IsNotificationFlyoutOpen && CurrentSection == "Home" && HomeMode == "Dashboard" && Devices.Count > 0 && !IsOnboardingClaimSuccess
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility UnreadNotificationBadgeVisibility => UnreadNotificationCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SidebarUnreadNotificationBadgeVisibility => IsSidebarExpanded && UnreadNotificationCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RegisterFieldVisibility => IsRegisterMode ? Visibility.Visible : Visibility.Collapsed;

    private Task ToggleAuthModeAsync()
    {
        IsRegisterMode = !IsRegisterMode;
        return Task.CompletedTask;
    }

    private Task ToggleSidebarAsync()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
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
        if (IsAuthenticated && section == "Home")
        {
            HomeMode = Devices.Count == 0 ? "AddDevice" : "Dashboard";
            await RefreshHomeAsync();
        }
        else if (IsAuthenticated && section == "Devices")
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
            AvailableDevices.Clear();
            HomeDeviceCards.Clear();
            Capabilities.Clear();
            Sensors.Clear();
            Actuators.Clear();
            LatestValues.Clear();
            TelemetryHistory.Clear();
            Commands.Clear();
            AutomationRules.Clear();
            AutomationRuleCards.Clear();
            AutomationExecutions.Clear();
            Notifications.Clear();
            NotifyDashboardCounts();
            StatusMessage = "Signed out.";
        });
    }

    private async Task RefreshAllAsync()
    {
        await RefreshHomeAsync();
        await RefreshAutomationRulesAsync();
    }

    private async Task RefreshHomeAsync()
    {
        await RunAsync(async () =>
        {
            await RefreshDevicesAsync();
            await BuildHomeDeviceCardsAsync();
            await RefreshAvailableDevicesSafelyAsync();
            await RefreshNotificationsAsync();
            if (HomeMode == "DeviceDetail" && SelectedDevice is not null)
            {
                await LoadDeviceDetailAsync();
            }

            LastHomeRefreshText = $"Last updated {DateTime.Now:t}";
        });
    }

    private Task SelectDeviceAsync(DeviceSummary? device)
    {
        if (device is not null)
        {
            SelectedDevice = device;
        }

        return Task.CompletedTask;
    }

    private Task SelectAvailableDeviceAsync(AvailableDevice? device)
    {
        if (device is not null)
        {
            SelectedAvailableDevice = device;
            AvailablePairingCode = "";
        }

        return Task.CompletedTask;
    }

    private Task SelectAutomationRuleAsync(AutomationRuleCard? card)
    {
        if (card is not null)
        {
            SelectedAutomationRule = card.Rule;
        }

        return Task.CompletedTask;
    }

    private async Task RefreshDevicesAsync()
    {
        await RunAsync(async () =>
        {
            var previousSelectedId = SelectedDevice?.Id;
            var devices = await _api.GetDevicesAsync();
            SyncByKey(Devices, devices, device => device.Id);
            var nextSelectedDevice = Devices.FirstOrDefault(device => device.Id == previousSelectedId) ?? Devices.FirstOrDefault();
            if (SelectedDevice?.Id == nextSelectedDevice?.Id)
            {
                _selectedDevice = nextSelectedDevice;
                NotifySelectedDeviceChanged();
            }
            else
            {
                SelectedDevice = nextSelectedDevice;
            }

            NotifyDashboardCounts();
            StatusMessage = Devices.Count == 0
                ? "No devices yet. Start a DeviceApp and claim it with a pairing code."
                : $"Loaded {Devices.Count} devices.";
        });
    }

    private async Task RefreshAvailableDevicesSafelyAsync()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        try
        {
            Replace(AvailableDevices, await _api.GetAvailableDevicesAsync());
            AvailableDeviceMessage = AvailableDevices.Count == 0
                ? "Waiting for DeviceApp..."
                : $"{AvailableDevices.Count} online device ready.";
            if (SelectedAvailableDevice is not null && AvailableDevices.All(device => device.DeviceId != SelectedAvailableDevice.DeviceId))
            {
                SelectedAvailableDevice = null;
                AvailablePairingCode = "";
            }
            if (SelectedAvailableDevice is null && AvailableDevices.Count > 0)
            {
                SelectedAvailableDevice = AvailableDevices[0];
            }
        }
        catch
        {
            AvailableDevices.Clear();
            AvailableDeviceMessage = "Available device discovery is temporarily unavailable.";
        }

        NotifyDashboardCounts();
    }

    private async Task BuildHomeDeviceCardsAsync()
    {
        var selectedCapabilityKey = SelectedHomeDeviceCard?.CapabilityKey;
        var selectedDeviceId = SelectedHomeDeviceCard?.DeviceId;
        var cards = new List<HomeDeviceCard>();
        foreach (var device in Devices)
        {
            var capabilities = await _api.GetDeviceCapabilitiesAsync(device.Id);
            var latestValues = await _api.GetLatestValuesAsync(device.Id);
            var latestByCapability = latestValues.ToDictionary(value => value.CapabilityKey, StringComparer.OrdinalIgnoreCase);
            foreach (var capability in capabilities.Where(capability => capability.Kind is "sensor" or "actuator"))
            {
                latestByCapability.TryGetValue(capability.Key, out var latest);
                cards.Add(HomeDeviceCard.From(device, capability, latest));
            }
        }

        SyncByKey(
            HomeDeviceCards,
            cards
            .OrderBy(card => card.IsOnline ? 0 : 1)
            .ThenBy(card => card.DeviceName)
            .ThenBy(card => card.KindSort)
            .ThenBy(card => card.CapabilityName),
            card => $"{card.DeviceId:N}:{card.CapabilityKey}",
            (existing, incoming) => existing.UpdateFrom(incoming));
        if (selectedDeviceId is not null && !string.IsNullOrWhiteSpace(selectedCapabilityKey))
        {
            SelectedHomeDeviceCard = HomeDeviceCards.FirstOrDefault(card =>
                card.DeviceId == selectedDeviceId && card.CapabilityKey.Equals(selectedCapabilityKey, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(AutomationSourceSelectionKey))
        {
            var firstSource = HomeDeviceCards.FirstOrDefault(card => card.IsSensor);
            if (firstSource is not null)
            {
                AutomationSourceSelectionKey = CreateAutomationSelectionKey(firstSource.DeviceId, firstSource.CapabilityKey);
            }
        }

        if (string.IsNullOrWhiteSpace(AutomationTargetSelectionKey))
        {
            var firstTarget = HomeDeviceCards.FirstOrDefault(card => card.IsActuator);
            if (firstTarget is not null)
            {
                AutomationTargetSelectionKey = CreateAutomationSelectionKey(firstTarget.DeviceId, firstTarget.CapabilityKey);
            }
        }

        RebuildAutomationRuleCards();
        NotifyDashboardCounts();
    }

    private Task SetHomeDeviceFilterAsync(string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            HomeDeviceFilter = filter;
        }

        return Task.CompletedTask;
    }

    private async Task OpenHomeDeviceAsync(HomeDeviceCard? card)
    {
        if (card is null)
        {
            return;
        }

        var device = Devices.FirstOrDefault(item => item.Id == card.DeviceId);
        if (device is null)
        {
            await RefreshDevicesAsync();
            device = Devices.FirstOrDefault(item => item.Id == card.DeviceId);
        }

        if (device is null)
        {
            StatusMessage = "Device is no longer available.";
            return;
        }

        IsNotificationFlyoutOpen = false;
        CurrentSection = "Home";
        SelectedHomeDeviceCard = card;
        IsHomeDeviceRenaming = false;
        SelectedDevice = device;
        HomeMode = "DeviceDetail";
        await LoadDeviceDetailAsync();
    }

    private Task BackToHomeDashboardAsync()
    {
        IsNotificationFlyoutOpen = false;
        HomeMode = "Dashboard";
        IsHomeDeviceRenaming = false;
        return Task.CompletedTask;
    }

    private Task StartHomeDeviceRenameAsync()
    {
        if (SelectedDevice is not null)
        {
            RenameDisplayName = SelectedDevice.DisplayName ?? SelectedDevice.NameOrSerial;
            OnPropertyChanged(nameof(RenameDisplayName));
            IsHomeDeviceRenaming = true;
        }

        return Task.CompletedTask;
    }

    private Task CancelHomeDeviceRenameAsync()
    {
        if (SelectedDevice is not null)
        {
            RenameDisplayName = SelectedDevice.DisplayName ?? "";
            OnPropertyChanged(nameof(RenameDisplayName));
        }

        IsHomeDeviceRenaming = false;
        return Task.CompletedTask;
    }

    private async Task OpenAddDeviceAsync()
    {
        IsNotificationFlyoutOpen = false;
        CurrentSection = "Home";
        HomeMode = "AddDevice";
        await RefreshAvailableDevicesSafelyAsync();
    }

    private async Task OpenCreateAutomationAsync()
    {
        IsNotificationFlyoutOpen = false;
        CurrentSection = "Automation";
        if (SelectedHomeDeviceCard is not null)
        {
            var selection = CreateAutomationSelectionKey(SelectedHomeDeviceCard.DeviceId, SelectedHomeDeviceCard.CapabilityKey);
            if (SelectedHomeDeviceCard.IsSensor)
            {
                AutomationSourceSelectionKey = selection;
            }
            else if (SelectedHomeDeviceCard.IsActuator)
            {
                AutomationTargetSelectionKey = selection;
            }
        }

        await RefreshAutomationRulesAsync();
    }

    private async Task ClaimDeviceAsync()
    {
        await RunAsync(async () =>
        {
            var device = await _api.ClaimDeviceAsync(ClaimPairingCode, ClaimDisplayName, EmptyToNull(ClaimCategory));
            await RefreshDevicesAsync();
            await RefreshAvailableDevicesSafelyAsync();
            SelectedDevice = Devices.FirstOrDefault(item => item.Id == device.Id);
            CurrentSection = "Home";
            HomeMode = "DeviceDetail";
            StatusMessage = $"Claimed {device.NameOrSerial}.";
        });
    }

    private async Task ClaimSelectedAvailableDeviceAsync()
    {
        if (SelectedAvailableDevice is null)
        {
            return;
        }

        var availableDevice = SelectedAvailableDevice;
        try
        {
            if (string.IsNullOrWhiteSpace(AvailablePairingCode))
            {
                AvailableClaimError = "Enter the pairing code shown in DeviceApp.";
                return;
            }

            AvailableClaimError = "";
            var device = await _api.ClaimDeviceAsync(AvailablePairingCode, availableDevice.KindLabel, EmptyToNull(availableDevice.DefaultCategory));
            AvailablePairingCode = "";
            SelectedAvailableDevice = null;
            await RefreshDevicesAsync();
            await RefreshAvailableDevicesSafelyAsync();
            SelectedDevice = Devices.FirstOrDefault(item => item.Id == device.Id);
            CurrentSection = "Home";
            IsOnboardingClaimSuccess = true;
            StatusMessage = $"Claimed {device.NameOrSerial}.";
            await Task.Delay(TimeSpan.FromMilliseconds(1400));
            IsOnboardingClaimSuccess = false;
            HomeMode = "DeviceDetail";
        }
        catch
        {
            AvailableClaimError = "Pairing code is invalid, expired, or already used.";
        }
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
            IsHomeDeviceRenaming = false;
            StatusMessage = "Device details saved.";
        });
    }

    private async Task UnclaimDeviceAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var device = SelectedDevice;
        var answer = MessageBox.Show(
            $"Remove {device.NameOrSerial} from your account? You can claim it again later with a valid pairing code.",
            "Remove device",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _api.UnclaimDeviceAsync(device.Id);
            SelectedDevice = null;
            await RefreshHomeAsync();
            HomeMode = Devices.Count == 0 ? "AddDevice" : "Dashboard";
            StatusMessage = $"Removed {device.NameOrSerial} from your account.";
        });
    }

    private async Task LoadDeviceDetailAsync()
    {
        if (SelectedDevice is null || !IsAuthenticated || _isRefreshingDeviceDetail)
        {
            return;
        }

        _isRefreshingDeviceDetail = true;
        try
        {
            await RunAsync(async () =>
            {
                var capabilities = await _api.GetDeviceCapabilitiesAsync(SelectedDevice.Id);
                SyncByKey(Capabilities, capabilities, capability => capability.Key);
                SyncByKey(LatestValues, await _api.GetLatestValuesAsync(SelectedDevice.Id), value => value.CapabilityKey);
                var history = await _api.GetTelemetryHistoryAsync(SelectedDevice.Id, EmptyToNull(HistoryCapabilityKey), pageSize: 100);
                SyncByKey(TelemetryHistory, history.Items, item => item.Id);
                BuildControlModels();
                BuildHomeDeviceTelemetryLog();
                StatusMessage = $"Loaded {SelectedDevice.NameOrSerial}.";
            });
        }
        finally
        {
            _isRefreshingDeviceDetail = false;
        }
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
        if (SelectedDevice is null || control is null || !SelectedDeviceIsOnline)
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

    private async Task ToggleHomeActuatorAsync(HomeDeviceCard? card)
    {
        if (card is null || !card.IsActuator)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var commandName = card.NextCommandName;
            var command = await _api.CreateCommandAsync(card.DeviceId, card.CapabilityKey, commandName, card.NextDesiredValue);
            Commands.Insert(0, command);
            card.MarkSubmitted(commandName, command.Status);
            StatusMessage = $"Sent {commandName.ToLowerInvariant()} to {card.CapabilityName}.";
        });
    }

    private async Task RefreshAutomationRulesAsync()
    {
        await RunAsync(async () =>
        {
            Replace(AutomationRules, await _api.GetAutomationRulesAsync());
            RebuildAutomationRuleCards();
            NotifyDashboardCounts();
            StatusMessage = $"Loaded {AutomationRules.Count} automation rules.";
        });
    }

    private async Task SaveAutomationRuleAsync()
    {
        await RunAsync(async () =>
        {
            var hasSource = !string.IsNullOrWhiteSpace(AutomationSourceSelectionKey);
            var sourceDeviceId = hasSource ? Guid.Parse(AutomationSourceDeviceId) : (Guid?)null;
            var sourceCapabilityKey = hasSource ? AutomationSourceCapabilityKey : null;
            var comparisonOperator = hasSource ? AutomationOperator : null;
            var thresholdValue = hasSource ? ParseDynamicValue(AutomationThresholdValue) : (JsonElement?)null;
            var targetDeviceId = Guid.Parse(AutomationTargetDeviceId);
            var cooldown = int.Parse(AutomationCooldownSeconds);
            var schedule = CreateAutomationSchedulePayload();
            if (SelectedAutomationRule is null)
            {
                var created = await _api.CreateAutomationRuleAsync(new CreateAutomationRulePayload(
                    AutomationName,
                    sourceDeviceId,
                    sourceCapabilityKey,
                    comparisonOperator,
                    thresholdValue,
                    targetDeviceId,
                    AutomationTargetCapabilityKey,
                    AutomationCommandName,
                    ParseDynamicValue(AutomationDesiredValue),
                    cooldown,
                    AutomationIsEnabled,
                    schedule.ActiveFromTime,
                    schedule.ActiveToTime,
                    schedule.ActiveDaysOfWeek));
                await RefreshAutomationRulesAsync();
                SelectedAutomationRule = AutomationRules.FirstOrDefault(item => item.Id == created.Id);
                StatusMessage = "Automation rule created.";
            }
            else
            {
                var updated = await _api.UpdateAutomationRuleAsync(SelectedAutomationRule.Id, new UpdateAutomationRulePayload(
                    AutomationName,
                    sourceDeviceId,
                    sourceCapabilityKey,
                    comparisonOperator,
                    thresholdValue,
                    targetDeviceId,
                    AutomationTargetCapabilityKey,
                    AutomationCommandName,
                    ParseDynamicValue(AutomationDesiredValue),
                    cooldown,
                    AutomationIsEnabled,
                    schedule.ActiveFromTime,
                    schedule.ActiveToTime,
                    schedule.ActiveDaysOfWeek));
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

    private Task ToggleNotificationFlyoutAsync()
    {
        IsNotificationFlyoutOpen = true;
        return Task.CompletedTask;
    }

    private async Task ViewAllNotificationsAsync()
    {
        IsNotificationFlyoutOpen = false;
        CurrentSection = "Notifications";
        await RefreshNotificationsAsync();
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

    private async Task MarkAllNotificationsReadAsync()
    {
        await RunAsync(async () =>
        {
            await _api.MarkAllNotificationsReadAsync();
            Replace(Notifications, await _api.GetNotificationsAsync());
            NotifyDashboardCounts();
            StatusMessage = "All alerts marked as read.";
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
            await RefreshAvailableDevicesSafelyAsync();
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
        SyncByKey(Sensors, Capabilities
            .Where(capability => capability.Kind == "sensor")
            .Select(capability =>
            {
                latestByCapability.TryGetValue(capability.Key, out var latest);
                return new SensorCard(
                    capability.Key,
                    capability.DisplayName,
                    capability.Unit,
                    latest?.DisplayValue ?? "No readings yet",
                    latest?.RecordedAtUtc.ToLocalTime().ToString("g") ?? "Waiting for telemetry",
                    SelectedDeviceIsOnline);
            }),
            card => card.Key,
            (existing, incoming) => existing.UpdateFrom(incoming));
        SyncByKey(Actuators, Capabilities
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
                    isOn ? "Currently on" : "Currently off",
                    SelectedDeviceIsOnline);
            }),
            control => control.Key,
            (existing, incoming) => existing.UpdateFrom(incoming));
        OnPropertyChanged(nameof(SelectedSensorCount));
        OnPropertyChanged(nameof(SelectedActuatorCount));
        OnPropertyChanged(nameof(SelectedDeviceLatestCount));
        OnPropertyChanged(nameof(SelectedDeviceTypeLabel));
        OnPropertyChanged(nameof(SelectedDeviceIconData));
    }

    private void BuildHomeDeviceTelemetryLog()
    {
        var capabilitiesByKey = Capabilities.ToDictionary(capability => capability.Key, StringComparer.OrdinalIgnoreCase);
        var previousActuatorValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seenSensorBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filtered = new List<HomeDeviceTelemetryLogItem>();

        foreach (var item in TelemetryHistory.OrderBy(item => item.RecordedAtUtc))
        {
            if (!capabilitiesByKey.TryGetValue(item.CapabilityKey, out var capability))
            {
                continue;
            }

            if (capability.Kind == "actuator")
            {
                var stateValue = item.DisplayValue;
                if (previousActuatorValues.TryGetValue(item.CapabilityKey, out var previousValue) &&
                    !string.Equals(previousValue, stateValue, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(HomeDeviceTelemetryLogItem.From(item, capability, FormatHomeLogValue(item, capability)));
                }

                previousActuatorValues[item.CapabilityKey] = stateValue;
                continue;
            }

            if (capability.Kind == "sensor")
            {
                var bucket = item.RecordedAtUtc.UtcDateTime.Ticks / TimeSpan.FromMinutes(5).Ticks;
                var bucketKey = $"{item.CapabilityKey}:{bucket}";
                if (seenSensorBuckets.Add(bucketKey))
                {
                    filtered.Add(HomeDeviceTelemetryLogItem.From(item, capability, FormatHomeLogValue(item, capability)));
                }
            }
        }

        SyncByKey(HomeDeviceTelemetryLog, filtered
            .OrderByDescending(item => item.RecordedAtUtc)
            .Take(100),
            item => $"{item.RecordedAtUtc:O}:{item.CapabilityName}:{item.Value}",
            (existing, incoming) => existing.UpdateFrom(incoming));
        OnPropertyChanged(nameof(EmptyHomeTelemetryLogVisibility));
        OnPropertyChanged(nameof(HasHomeTelemetryLogVisibility));
    }

    private static string FormatHomeLogValue(TelemetryHistoryItem item, DeviceCapability capability)
    {
        if (capability.Kind == "actuator" && item.ValueType == "boolean")
        {
            return item.BooleanValue == true ? "On" : "Off";
        }

        if (capability.Kind == "sensor" && !string.IsNullOrWhiteSpace(capability.Unit) && !string.IsNullOrWhiteSpace(item.DisplayValue))
        {
            return $"{item.DisplayValue} {capability.Unit}";
        }

        return item.DisplayValue;
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
        RefreshHomeCommand.RaiseCanExecuteChanged();
        SetHomeDeviceFilterCommand.RaiseCanExecuteChanged();
        OpenHomeDeviceCommand.RaiseCanExecuteChanged();
        BackToHomeDashboardCommand.RaiseCanExecuteChanged();
        OpenAddDeviceCommand.RaiseCanExecuteChanged();
        OpenCreateAutomationCommand.RaiseCanExecuteChanged();
        StartHomeDeviceRenameCommand.RaiseCanExecuteChanged();
        CancelHomeDeviceRenameCommand.RaiseCanExecuteChanged();
        ToggleHomeActuatorCommand.RaiseCanExecuteChanged();
        SelectDeviceCommand.RaiseCanExecuteChanged();
        SelectAvailableDeviceCommand.RaiseCanExecuteChanged();
        ClaimSelectedAvailableDeviceCommand.RaiseCanExecuteChanged();
        ClaimDeviceCommand.RaiseCanExecuteChanged();
        RenameDeviceCommand.RaiseCanExecuteChanged();
        UnclaimDeviceCommand.RaiseCanExecuteChanged();
        LoadDeviceDetailCommand.RaiseCanExecuteChanged();
        SendCommandCommand.RaiseCanExecuteChanged();
        SendActuatorCommand.RaiseCanExecuteChanged();
        RefreshAutomationRulesCommand.RaiseCanExecuteChanged();
        RefreshNotificationsCommand.RaiseCanExecuteChanged();
        MarkAllNotificationsReadCommand.RaiseCanExecuteChanged();
        ToggleNotificationFlyoutCommand.RaiseCanExecuteChanged();
        ToggleSidebarCommand.RaiseCanExecuteChanged();
        ViewAllNotificationsCommand.RaiseCanExecuteChanged();
        SelectAutomationRuleCommand.RaiseCanExecuteChanged();
        SaveAutomationRuleCommand.RaiseCanExecuteChanged();
        DeleteAutomationRuleCommand.RaiseCanExecuteChanged();
        LoadAutomationExecutionsCommand.RaiseCanExecuteChanged();
    }

    private void NotifySelectedDeviceChanged()
    {
        RaiseCommandStates();
        OnPropertyChanged(nameof(SelectedDeviceName));
        OnPropertyChanged(nameof(SelectedDeviceStatus));
        OnPropertyChanged(nameof(SelectedDeviceTitle));
        OnPropertyChanged(nameof(SelectedDeviceSubtitle));
        OnPropertyChanged(nameof(SelectedDeviceStatusBrush));
        OnPropertyChanged(nameof(SelectedDeviceStatusBackground));
        OnPropertyChanged(nameof(SelectedDeviceIsOnline));
        OnPropertyChanged(nameof(SelectedDeviceContentBrush));
        OnPropertyChanged(nameof(SelectedDeviceMutedBrush));
        OnPropertyChanged(nameof(SelectedDeviceSurfaceBrush));
        OnPropertyChanged(nameof(SelectedDeviceIconBrush));
        OnPropertyChanged(nameof(SelectedDeviceTypeLabel));
        OnPropertyChanged(nameof(SelectedDeviceIconData));
        OnPropertyChanged(nameof(HasSelectedDeviceVisibility));
        OnPropertyChanged(nameof(NoSelectedDeviceVisibility));
        OnPropertyChanged(nameof(HomeDeviceDetailVisibility));
    }

    private void NotifyDashboardCounts()
    {
        OnPropertyChanged(nameof(OnlineDeviceCount));
        OnPropertyChanged(nameof(OfflineDeviceCount));
        OnPropertyChanged(nameof(UnreadNotificationCount));
        OnPropertyChanged(nameof(SelectedSensorCount));
        OnPropertyChanged(nameof(SelectedActuatorCount));
        OnPropertyChanged(nameof(HomeSensorCount));
        OnPropertyChanged(nameof(HomeActuatorCount));
        OnPropertyChanged(nameof(FilteredHomeDeviceCards));
        OnPropertyChanged(nameof(HomeViewAllDevicesVisibility));
        OnPropertyChanged(nameof(AutomationSourceOptions));
        OnPropertyChanged(nameof(AutomationTargetOptions));
        OnPropertyChanged(nameof(UnreadNotificationBadgeText));
        OnPropertyChanged(nameof(UnreadNotificationBadgeVisibility));
        OnPropertyChanged(nameof(SidebarUnreadNotificationBadgeVisibility));
        OnPropertyChanged(nameof(RecentHomeNotifications));
        OnPropertyChanged(nameof(RecentActivityNotifications));
        OnPropertyChanged(nameof(EnabledAutomationCount));
        OnPropertyChanged(nameof(AutomationRuleCards));
        OnPropertyChanged(nameof(EmptyDevicesVisibility));
        OnPropertyChanged(nameof(HasDevicesVisibility));
        OnPropertyChanged(nameof(HomeEmptyVisibility));
        OnPropertyChanged(nameof(HomeDashboardVisibility));
        OnPropertyChanged(nameof(HomeAddDeviceVisibility));
        OnPropertyChanged(nameof(HomeDeviceDetailVisibility));
        OnPropertyChanged(nameof(HomeNotificationFlyoutVisibility));
        OnPropertyChanged(nameof(EmptyAvailableDevicesVisibility));
        OnPropertyChanged(nameof(HasAvailableDevicesVisibility));
        OnPropertyChanged(nameof(OnboardingWaitingVisibility));
        OnPropertyChanged(nameof(OnboardingClaimVisibility));
        OnPropertyChanged(nameof(OnboardingSuccessVisibility));
        OnPropertyChanged(nameof(OnboardingTitle));
        OnPropertyChanged(nameof(OnboardingSubtitle));
        OnPropertyChanged(nameof(OnboardingHeroIcon));
        OnPropertyChanged(nameof(OnboardingHeroIconForeground));
        OnPropertyChanged(nameof(OnboardingHeroIconBackground));
        OnPropertyChanged(nameof(IsOnboardingDeviceDetected));
        OnPropertyChanged(nameof(Step2DotText));
        OnPropertyChanged(nameof(Step3DotText));
        OnPropertyChanged(nameof(Step4DotText));
        OnPropertyChanged(nameof(Step2Background));
        OnPropertyChanged(nameof(Step2Border));
        OnPropertyChanged(nameof(Step2Foreground));
        OnPropertyChanged(nameof(Step3Background));
        OnPropertyChanged(nameof(Step3Border));
        OnPropertyChanged(nameof(Step3Foreground));
        OnPropertyChanged(nameof(Step4Background));
        OnPropertyChanged(nameof(Step4Border));
        OnPropertyChanged(nameof(Step4Foreground));
        OnPropertyChanged(nameof(Step2LabelForeground));
        OnPropertyChanged(nameof(Step3LabelForeground));
        OnPropertyChanged(nameof(Step4LabelForeground));
        OnPropertyChanged(nameof(Step2ConnectorBrush));
        OnPropertyChanged(nameof(Step3ConnectorBrush));
        OnPropertyChanged(nameof(SelectedAvailableDeviceVisibility));
        OnPropertyChanged(nameof(EmptyNotificationsVisibility));
        OnPropertyChanged(nameof(HasNotificationsVisibility));
        OnPropertyChanged(nameof(EmptyHomeTelemetryLogVisibility));
        OnPropertyChanged(nameof(HasHomeTelemetryLogVisibility));
        MarkAllNotificationsReadCommand.RaiseCanExecuteChanged();
    }

    private void OnAutomationPropertiesChanged()
    {
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(AutomationSourceDeviceId));
        OnPropertyChanged(nameof(AutomationSourceCapabilityKey));
        OnPropertyChanged(nameof(AutomationSourceSelectionKey));
        OnPropertyChanged(nameof(AutomationOperator));
        OnPropertyChanged(nameof(AutomationThresholdValue));
        OnPropertyChanged(nameof(AutomationTargetDeviceId));
        OnPropertyChanged(nameof(AutomationTargetCapabilityKey));
        OnPropertyChanged(nameof(AutomationTargetSelectionKey));
        OnPropertyChanged(nameof(AutomationCommandName));
        OnPropertyChanged(nameof(AutomationDesiredValue));
        OnPropertyChanged(nameof(AutomationCooldownSeconds));
        OnPropertyChanged(nameof(AutomationActiveFromTime));
        OnPropertyChanged(nameof(AutomationActiveToTime));
        OnPropertyChanged(nameof(AutomationActiveDaysOfWeek));
        OnPropertyChanged(nameof(AutomationIsEnabled));
        OnPropertyChanged(nameof(IsAutomationScheduleEnabled));
        OnPropertyChanged(nameof(AutomationScheduleVisibility));
        OnPropertyChanged(nameof(IsAutomationTriggerConfigured));
        OnPropertyChanged(nameof(IsAutomationConditionEnabled));
        OnPropertyChanged(nameof(AutomationAllDays));
        OnPropertyChanged(nameof(AutomationSunday));
        OnPropertyChanged(nameof(AutomationMonday));
        OnPropertyChanged(nameof(AutomationTuesday));
        OnPropertyChanged(nameof(AutomationWednesday));
        OnPropertyChanged(nameof(AutomationThursday));
        OnPropertyChanged(nameof(AutomationFriday));
        OnPropertyChanged(nameof(AutomationSaturday));
    }

    private void RebuildAutomationRuleCards()
    {
        var cards = AutomationRules.Select(rule =>
        {
            var source = ResolveAutomationEndpoint(rule.SourceDeviceId, rule.SourceDeviceSerial, rule.SourceCapabilityKey);
            var target = ResolveAutomationEndpoint(rule.TargetDeviceId, rule.TargetDeviceSerial, rule.TargetCapabilityKey);
            return AutomationRuleCard.From(rule, source, target, FormatScheduleSummary(rule));
        });

        Replace(AutomationRuleCards, cards);
    }

    private string ResolveAutomationEndpoint(Guid? deviceId, string? fallbackDeviceName, string? capabilityKey)
    {
        if (deviceId is null || string.IsNullOrWhiteSpace(capabilityKey))
        {
            return "Schedule only";
        }

        var card = HomeDeviceCards.FirstOrDefault(item =>
            item.DeviceId == deviceId.Value && item.CapabilityKey.Equals(capabilityKey, StringComparison.OrdinalIgnoreCase));
        if (card is not null)
        {
            return $"{card.DeviceName} - {card.CapabilityName}";
        }

        var deviceName = Devices.FirstOrDefault(device => device.Id == deviceId.Value)?.NameOrSerial;
        return $"{(string.IsNullOrWhiteSpace(deviceName) ? fallbackDeviceName : deviceName)} - {FormatCapabilityName(capabilityKey)}";
    }

    private void SetAutomationScheduleFromRule(AutomationRuleDto rule)
    {
        IsAutomationScheduleEnabled = !string.IsNullOrWhiteSpace(rule.ActiveFromTime)
            || !string.IsNullOrWhiteSpace(rule.ActiveToTime)
            || rule.ActiveDaysOfWeek is not null;

        AutomationActiveFromTime = ToLocalScheduleTime(rule.ActiveFromTime);
        AutomationActiveToTime = ToLocalScheduleTime(rule.ActiveToTime);
        AutomationActiveDaysOfWeek = rule.ActiveDaysOfWeek?.ToString() ?? "";
        SetScheduleDayValues(ToLocalDayMask(rule.ActiveDaysOfWeek, rule.ActiveFromTime));
    }

    private AutomationSchedulePayload CreateAutomationSchedulePayload()
    {
        if (!IsAutomationScheduleEnabled)
        {
            return new AutomationSchedulePayload(null, null, null);
        }

        var localFrom = EmptyToNull(AutomationActiveFromTime) ?? "00:00";
        var localTo = EmptyToNull(AutomationActiveToTime) ?? "23:59";
        var utcFrom = ToUtcScheduleTime(localFrom);
        var utcTo = ToUtcScheduleTime(localTo);
        var utcDays = ToUtcDayMask(GetLocalScheduleDayMask(), localFrom);
        return new AutomationSchedulePayload(utcFrom, utcTo, utcDays == 0 ? null : utcDays);
    }

    private string FormatScheduleSummary(AutomationRuleDto rule)
    {
        if (string.IsNullOrWhiteSpace(rule.ActiveFromTime) && string.IsNullOrWhiteSpace(rule.ActiveToTime) && rule.ActiveDaysOfWeek is null)
        {
            return $"Every time - {rule.CooldownSeconds}s cooldown";
        }

        var from = ToLocalScheduleTime(rule.ActiveFromTime);
        var to = ToLocalScheduleTime(rule.ActiveToTime);
        var days = FormatDayMask(ToLocalDayMask(rule.ActiveDaysOfWeek, rule.ActiveFromTime));
        var range = string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)
            ? "All day"
            : $"{(string.IsNullOrWhiteSpace(from) ? "00:00" : from)}-{(string.IsNullOrWhiteSpace(to) ? "23:59" : to)}";
        return $"{range}, {days} - {rule.CooldownSeconds}s cooldown";
    }

    private void SetScheduleDay(ref bool field, bool value, string propertyName)
    {
        if (SetProperty(ref field, value))
        {
            AutomationActiveDaysOfWeek = GetLocalScheduleDayMask().ToString();
            OnPropertyChanged(nameof(AutomationActiveDaysOfWeek));
            OnPropertyChanged(nameof(AutomationAllDays));
            OnPropertyChanged(propertyName);
        }
    }

    private void SetScheduleDayValues(int dayMask)
    {
        _automationSunday = HasDay(dayMask, DayOfWeek.Sunday);
        _automationMonday = HasDay(dayMask, DayOfWeek.Monday);
        _automationTuesday = HasDay(dayMask, DayOfWeek.Tuesday);
        _automationWednesday = HasDay(dayMask, DayOfWeek.Wednesday);
        _automationThursday = HasDay(dayMask, DayOfWeek.Thursday);
        _automationFriday = HasDay(dayMask, DayOfWeek.Friday);
        _automationSaturday = HasDay(dayMask, DayOfWeek.Saturday);
    }

    private int GetLocalScheduleDayMask()
    {
        var mask = 0;
        if (AutomationSunday) mask |= DayBit(DayOfWeek.Sunday);
        if (AutomationMonday) mask |= DayBit(DayOfWeek.Monday);
        if (AutomationTuesday) mask |= DayBit(DayOfWeek.Tuesday);
        if (AutomationWednesday) mask |= DayBit(DayOfWeek.Wednesday);
        if (AutomationThursday) mask |= DayBit(DayOfWeek.Thursday);
        if (AutomationFriday) mask |= DayBit(DayOfWeek.Friday);
        if (AutomationSaturday) mask |= DayBit(DayOfWeek.Saturday);
        return mask == 0 ? 127 : mask;
    }

    private static bool HasDay(int mask, DayOfWeek day) => (mask & DayBit(day)) != 0;

    private static int DayBit(DayOfWeek day) => 1 << (int)day;

    private static string ToUtcScheduleTime(string localTime)
    {
        var time = TimeSpan.Parse(localTime);
        var local = new DateTimeOffset(DateTime.Today.Add(time), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today.Add(time)));
        return local.ToUniversalTime().ToString("HH:mm");
    }

    private static string ToLocalScheduleTime(string? utcTime)
    {
        if (string.IsNullOrWhiteSpace(utcTime))
        {
            return "";
        }

        var time = TimeSpan.Parse(utcTime);
        var utc = new DateTimeOffset(DateTime.UtcNow.Date.Add(time), TimeSpan.Zero);
        return utc.ToLocalTime().ToString("HH:mm");
    }

    private static int ToUtcDayMask(int localDayMask, string localFromTime)
    {
        var time = TimeSpan.Parse(localFromTime);
        var mask = 0;
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (!HasDay(localDayMask, day))
            {
                continue;
            }

            var localDate = DateForDay(DateTime.Today, day).Add(time);
            var local = new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
            mask |= DayBit(local.ToUniversalTime().DayOfWeek);
        }

        return mask;
    }

    private static int ToLocalDayMask(int? utcDayMask, string? utcFromTime)
    {
        if (utcDayMask is null)
        {
            return 127;
        }

        var time = TimeSpan.Parse(string.IsNullOrWhiteSpace(utcFromTime) ? "00:00" : utcFromTime);
        var mask = 0;
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            if (!HasDay(utcDayMask.Value, day))
            {
                continue;
            }

            var utc = new DateTimeOffset(DateForDay(DateTime.UtcNow.Date, day).Add(time), TimeSpan.Zero);
            mask |= DayBit(utc.ToLocalTime().DayOfWeek);
        }

        return mask;
    }

    private static DateTime DateForDay(DateTime start, DayOfWeek day)
    {
        var offset = ((int)day - (int)start.DayOfWeek + 7) % 7;
        return start.Date.AddDays(offset);
    }

    private static string FormatDayMask(int dayMask)
    {
        if (dayMask == 127)
        {
            return "Every day";
        }

        var parts = new List<string>();
        if (HasDay(dayMask, DayOfWeek.Monday)) parts.Add("Mon");
        if (HasDay(dayMask, DayOfWeek.Tuesday)) parts.Add("Tue");
        if (HasDay(dayMask, DayOfWeek.Wednesday)) parts.Add("Wed");
        if (HasDay(dayMask, DayOfWeek.Thursday)) parts.Add("Thu");
        if (HasDay(dayMask, DayOfWeek.Friday)) parts.Add("Fri");
        if (HasDay(dayMask, DayOfWeek.Saturday)) parts.Add("Sat");
        if (HasDay(dayMask, DayOfWeek.Sunday)) parts.Add("Sun");
        return parts.Count == 0 ? "Every day" : string.Join(", ", parts);
    }

    private static string FormatCapabilityName(string key) =>
        string.Join(" ", key.Split(['_', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static IReadOnlyList<string> BuildTimeOptions()
    {
        var values = new List<string>(96);
        for (var minutes = 0; minutes < 24 * 60; minutes += 15)
        {
            values.Add(TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm"));
        }

        return values;
    }

    private void ApplyAutomationSelection(string selectionKey, bool isSource)
    {
        if (!TryParseAutomationSelectionKey(selectionKey, out var deviceId, out var capabilityKey))
        {
            if (isSource)
            {
                AutomationSourceDeviceId = "";
                AutomationSourceCapabilityKey = "";
                AutomationOperator = "";
                IsAutomationScheduleEnabled = true;
                OnPropertyChanged(nameof(AutomationSourceDeviceId));
                OnPropertyChanged(nameof(AutomationSourceCapabilityKey));
                OnPropertyChanged(nameof(AutomationOperator));
            }

            return;
        }

        if (isSource)
        {
            AutomationSourceDeviceId = deviceId.ToString();
            AutomationSourceCapabilityKey = capabilityKey;
            if (string.IsNullOrWhiteSpace(AutomationOperator))
            {
                AutomationOperator = ">";
            }
            OnPropertyChanged(nameof(AutomationSourceDeviceId));
            OnPropertyChanged(nameof(AutomationSourceCapabilityKey));
        }
        else
        {
            AutomationTargetDeviceId = deviceId.ToString();
            AutomationTargetCapabilityKey = capabilityKey;
            OnPropertyChanged(nameof(AutomationTargetDeviceId));
            OnPropertyChanged(nameof(AutomationTargetCapabilityKey));
        }
    }

    private static string CreateAutomationSelectionKey(Guid deviceId, string capabilityKey) =>
        string.IsNullOrWhiteSpace(capabilityKey) ? "" : $"{deviceId:N}|{capabilityKey}";

    private static bool TryParseAutomationSelectionKey(string selectionKey, out Guid deviceId, out string capabilityKey)
    {
        deviceId = Guid.Empty;
        capabilityKey = "";
        var parts = selectionKey.Split('|', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out deviceId) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        capabilityKey = parts[1];
        return true;
    }

    private Visibility Section(string section) => CurrentSection == section ? Visibility.Visible : Visibility.Collapsed;

    private static string NavBackground(bool isSelected) => isSelected ? "#EFF6FF" : "Transparent";

    private static string NavForeground(bool isSelected) => isSelected ? "#2563EB" : "#64748B";

    private static double NavRailOpacity(bool isSelected) => isSelected ? 1 : 0;

    private string FilterBackground(string filter) => HomeDeviceFilter == filter ? "#2563EB" : "#FFFFFF";

    private string FilterForeground(string filter) => HomeDeviceFilter == filter ? "#FFFFFF" : "#64748B";

    private string ResolveSelectedDeviceTypeLabel()
    {
        if (!string.IsNullOrWhiteSpace(SelectedHomeDeviceCard?.CapabilityName))
        {
            return SelectedHomeDeviceCard.CapabilityName;
        }

        var seed = ResolveSelectedDeviceSeed();
        if (seed.Contains("pump"))
        {
            return "Pump";
        }

        if (seed.Contains("fan"))
        {
            return "Fan";
        }

        if (seed.Contains("light") || seed.Contains("lamp"))
        {
            return "Light";
        }

        if (seed.Contains("door") || seed.Contains("lock"))
        {
            return "Door lock";
        }

        if (seed.Contains("switch") || seed.Contains("power"))
        {
            return "Power switch";
        }

        if (Capabilities.Any(capability => capability.Kind == "sensor") && Capabilities.All(capability => capability.Kind != "actuator"))
        {
            return "Sensor";
        }

        return "Device";
    }

    private string ResolveSelectedDeviceSeed()
    {
        if (SelectedHomeDeviceCard is not null)
        {
            return $"{SelectedHomeDeviceCard.CapabilityKey} {SelectedHomeDeviceCard.CapabilityName} {SelectedHomeDeviceCard.Kind}".ToLowerInvariant();
        }

        var capabilityText = string.Join(' ', Capabilities.Select(capability => $"{capability.Key} {capability.DisplayName} {capability.Unit}"));
        return $"{SelectedDevice?.Category} {SelectedDevice?.NameOrSerial} {capabilityText}".ToLowerInvariant();
    }

    private static string ResolveDeviceIconData(string seed)
    {
        if (seed.Contains("pump") || seed.Contains("water"))
        {
            return "M2,7 A2,2 0 0 1 4,5 L13,5 A2,2 0 0 1 15,7 L15,17 A2,2 0 0 1 13,19 L4,19 A2,2 0 0 1 2,17 Z M2,8 A4,4 0 0 0 2,16 M5,8 L12,8 M5,11 L12,11 M5,14 L12,14 M5,16 L12,16 M15,8 L18,9 L18,15 L15,16 M18,10 L20,10 L20,14 L18,14 M20,12 L23,12";
        }

        if (seed.Contains("fan"))
        {
            return "M10.827,16.379 A6.082,6.082 0 0 1 2.209,9.377 L7.621,10.827 A6.082,6.082 0 0 1 14.623,2.209 L13.173,7.621 A6.082,6.082 0 0 1 21.791,14.623 L16.379,13.173 A6.082,6.082 0 0 1 9.377,21.791 Z M12,12 L12,12.01";
        }

        if (seed.Contains("light") || seed.Contains("lamp"))
        {
            return "M15,14 C15.2,13 15.7,12.3 16.5,11.5 C17.5,10.6 18,9.3 18,8 A6,6 0 0 0 6,8 C6,9 6.2,10.2 7.5,11.5 C8.2,12.2 8.8,13 9,14 M9,18 L15,18 M10,22 L14,22";
        }

        if (seed.Contains("door") || seed.Contains("lock"))
        {
            return "M10,12 L10.01,12 M18,9 L18,6 A2,2 0 0 0 16,4 L8,4 A2,2 0 0 0 6,6 L6,20 M2,20 L10,20 M20,17 L20,15 A2,2 0 1 0 16,15 L16,17 M15,17 L21,17 A1,1 0 0 1 22,18 L22,21 A1,1 0 0 1 21,22 L15,22 A1,1 0 0 1 14,21 L14,18 A1,1 0 0 1 15,17";
        }

        if (seed.Contains("switch") || seed.Contains("power"))
        {
            return "M12,2 A10,10 0 1 1 11.99,2 M12,7 L12,11 M7.998,9.003 A5,5 0 1 0 15.998,8.998";
        }

        return "M8.464,15.536 C6.512,13.583 6.512,10.417 8.464,8.464 M5.636,18.364 C2.121,14.849 2.121,9.151 5.636,5.636 M15.536,15.536 C17.488,13.583 17.488,10.417 15.536,8.464 M18.364,18.364 C21.879,14.849 21.879,9.151 18.364,5.636 M13,12 C13,12.552 12.552,13 12,13 C11.448,13 11,12.552 11,12 C11,11.448 11.448,11 12,11 C12.552,11 13,11.448 13,12";
    }

    private static string ToTitleLabel(string value)
    {
        var words = value.Replace('_', ' ').Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "Device";
        }

        return string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static void SyncByKey<T, TKey>(
        ObservableCollection<T> target,
        IEnumerable<T> values,
        Func<T, TKey> keySelector)
        where TKey : notnull
    {
        SyncByKey(target, values, keySelector, null);
    }

    private static void SyncByKey<T, TKey>(
        ObservableCollection<T> target,
        IEnumerable<T> values,
        Func<T, TKey> keySelector,
        Action<T, T>? updateExisting)
        where TKey : notnull
    {
        var desired = values.ToList();
        var desiredKeys = new HashSet<TKey>(desired.Select(keySelector));

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredKeys.Contains(keySelector(target[index])))
            {
                target.RemoveAt(index);
            }
        }

        for (var desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
        {
            var incoming = desired[desiredIndex];
            var key = keySelector(incoming);
            var existingIndex = IndexOfKey(target, keySelector, key);
            if (existingIndex < 0)
            {
                target.Insert(desiredIndex, incoming);
                continue;
            }

            var existing = target[existingIndex];
            updateExisting?.Invoke(existing, incoming);
            if (updateExisting is null && !EqualityComparer<T>.Default.Equals(existing, incoming))
            {
                target[existingIndex] = incoming;
                existing = incoming;
            }

            if (existingIndex != desiredIndex)
            {
                target.Move(existingIndex, desiredIndex);
            }
        }
    }

    private static int IndexOfKey<T, TKey>(ObservableCollection<T> target, Func<T, TKey> keySelector, TKey key)
        where TKey : notnull
    {
        for (var index = 0; index < target.Count; index++)
        {
            if (EqualityComparer<TKey>.Default.Equals(keySelector(target[index]), key))
            {
                return index;
            }
        }

        return -1;
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

    private static string FormatValue(string? valueType, decimal? numberValue, bool? booleanValue, string? stringValue) => valueType switch
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

public sealed record AutomationCapabilityOption(
    string SelectionKey,
    string DisplayName,
    Guid DeviceId,
    string CapabilityKey)
{
    public static AutomationCapabilityOption Empty { get; } = new("", "No trigger - schedule only", Guid.Empty, "");

    public static AutomationCapabilityOption From(HomeDeviceCard card) => new(
        $"{card.DeviceId:N}|{card.CapabilityKey}",
        $"{card.CapabilityName} - {card.DeviceName}",
        card.DeviceId,
        card.CapabilityKey);

    public override string ToString() => DisplayName;
}

public sealed record AutomationSchedulePayload(string? ActiveFromTime, string? ActiveToTime, int? ActiveDaysOfWeek);

public sealed record AutomationRuleCard(
    AutomationRuleDto Rule,
    string Name,
    string TriggerSummary,
    string ActionSummary,
    string MetaSummary,
    string EnabledText,
    string EnabledBackground,
    string EnabledForeground)
{
    public static AutomationRuleCard From(AutomationRuleDto rule, string source, string target, string scheduleSummary)
    {
        var threshold = FormatRuleValue(rule.ThresholdValueType, rule.ThresholdNumberValue, rule.ThresholdBooleanValue, rule.ThresholdStringValue);
        var trigger = string.IsNullOrWhiteSpace(rule.SourceCapabilityKey)
            ? "Schedule only"
            : $"{source} {rule.ComparisonOperator} {threshold}";
        return new AutomationRuleCard(
            rule,
            rule.Name,
            trigger,
            $"{target} -> {FormatCommandName(rule.CommandName)}",
            scheduleSummary,
            rule.IsEnabled ? "Enabled" : "Paused",
            rule.IsEnabled ? "#DCFCE7" : "#F1F5F9",
            rule.IsEnabled ? "#16A34A" : "#64748B");
    }

    private static string FormatCommandName(string commandName) => commandName.Equals("ON", StringComparison.OrdinalIgnoreCase)
        ? "Turn on"
        : commandName.Equals("OFF", StringComparison.OrdinalIgnoreCase)
            ? "Turn off"
            : commandName;

    private static string FormatRuleValue(string? valueType, decimal? numberValue, bool? booleanValue, string? stringValue) => valueType switch
    {
        "number" => numberValue?.ToString("0.###") ?? "",
        "boolean" => booleanValue == true ? "On" : "Off",
        "string" => stringValue ?? "",
        _ => ""
    };
}

public sealed class HomeDeviceTelemetryLogItem(
    string recorded,
    string capabilityName,
    string value,
    DateTimeOffset recordedAtUtc) : ObservableObject
{
    private string _recorded = recorded;
    private string _capabilityName = capabilityName;
    private string _value = value;

    public string Recorded
    {
        get => _recorded;
        private set => SetProperty(ref _recorded, value);
    }

    public string CapabilityName
    {
        get => _capabilityName;
        private set => SetProperty(ref _capabilityName, value);
    }

    public string Value
    {
        get => _value;
        private set => SetProperty(ref _value, value);
    }

    public DateTimeOffset RecordedAtUtc { get; } = recordedAtUtc;

    public static HomeDeviceTelemetryLogItem From(TelemetryHistoryItem item, DeviceCapability capability, string value) =>
        new(
            item.RecordedAtUtc.ToLocalTime().ToString("g"),
            capability.DisplayName,
            value,
            item.RecordedAtUtc);

    public void UpdateFrom(HomeDeviceTelemetryLogItem incoming)
    {
        Recorded = incoming.Recorded;
        CapabilityName = incoming.CapabilityName;
        Value = incoming.Value;
    }
}

public sealed class SensorCard(
    string key,
    string displayName,
    string? unit,
    string value,
    string recordedAt,
    bool isDeviceOnline) : ObservableObject
{
    private string _displayName = displayName;
    private string? _unit = unit;
    private string _value = value;
    private string _recordedAt = recordedAt;
    private bool _isDeviceOnline = isDeviceOnline;

    public string Key { get; } = key;

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string? Unit
    {
        get => _unit;
        private set
        {
            if (SetProperty(ref _unit, value))
            {
                OnPropertyChanged(nameof(ValueWithUnit));
            }
        }
    }

    public string Value
    {
        get => _value;
        private set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(ValueWithUnit));
            }
        }
    }

    public string RecordedAt
    {
        get => _recordedAt;
        private set => SetProperty(ref _recordedAt, value);
    }

    public bool IsDeviceOnline
    {
        get => _isDeviceOnline;
        private set
        {
            if (SetProperty(ref _isDeviceOnline, value))
            {
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(ContentBrush));
                OnPropertyChanged(nameof(IconBrush));
                OnPropertyChanged(nameof(IconBackground));
            }
        }
    }

    public string ValueWithUnit => string.IsNullOrWhiteSpace(Unit) || Value == "No readings yet" ? Value : $"{Value} {Unit}";
    public string CardBackground => IsDeviceOnline ? "#F8FAFC" : "#F1F5F9";
    public string CardBorderBrush => IsDeviceOnline ? "#D8E0EA" : "#E2E8F0";
    public string ContentBrush => IsDeviceOnline ? "#0F172A" : "#94A3B8";
    public string MutedBrush => "#94A3B8";
    public string IconBrush => IsDeviceOnline ? "#2563EB" : "#94A3B8";
    public string IconBackground => IsDeviceOnline ? "#EFF6FF" : "#E2E8F0";

    public void UpdateFrom(SensorCard incoming)
    {
        DisplayName = incoming.DisplayName;
        Unit = incoming.Unit;
        Value = incoming.Value;
        RecordedAt = incoming.RecordedAt;
        IsDeviceOnline = incoming.IsDeviceOnline;
    }
}

public sealed class HomeDeviceCard(
    Guid deviceId,
    string deviceName,
    string deviceStatus,
    string capabilityKey,
    string capabilityName,
    string kind,
    string dataType,
    string? unit,
    IReadOnlyList<string> supportedCommands,
    string value,
    string recordedAt,
    bool isOn) : ObservableObject
{
    private bool _isOn = isOn;
    private string _lastStatus = recordedAt;
    private string _deviceName = deviceName;
    private string _deviceStatus = deviceStatus;
    private string _value = value;
    private IReadOnlyList<string> _supportedCommands = supportedCommands;

    public Guid DeviceId { get; } = deviceId;
    public string DeviceName
    {
        get => _deviceName;
        private set => SetProperty(ref _deviceName, value);
    }

    public string DeviceStatus
    {
        get => _deviceStatus;
        private set
        {
            if (SetProperty(ref _deviceStatus, value))
            {
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(PrimaryBrush));
                OnPropertyChanged(nameof(MutedBrush));
                OnPropertyChanged(nameof(IconBrush));
                OnPropertyChanged(nameof(IconBackground));
                OnPropertyChanged(nameof(PrimaryValue));
                OnPropertyChanged(nameof(SecondaryText));
                OnPropertyChanged(nameof(SwitchBackground));
                OnPropertyChanged(nameof(SwitchLabelBrush));
            }
        }
    }

    public string CapabilityKey { get; } = capabilityKey;
    public string CapabilityName { get; } = capabilityName;
    public string Kind { get; } = kind;
    public string DataType { get; } = dataType;
    public string? Unit { get; } = unit;
    public IReadOnlyList<string> SupportedCommands
    {
        get => _supportedCommands;
        private set
        {
            if (SetProperty(ref _supportedCommands, value))
            {
                OnPropertyChanged(nameof(NextCommandName));
            }
        }
    }

    public string Value
    {
        get => _value;
        private set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(ValueWithUnit));
                OnPropertyChanged(nameof(PrimaryValue));
                OnPropertyChanged(nameof(SensorFooterText));
            }
        }
    }
    public bool IsSensor => Kind.Equals("sensor", StringComparison.OrdinalIgnoreCase);
    public bool IsActuator => Kind.Equals("actuator", StringComparison.OrdinalIgnoreCase);
    public Visibility ActuatorActionVisibility => IsActuator ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SensorInfoVisibility => IsSensor ? Visibility.Visible : Visibility.Collapsed;
    public bool IsOnline => DeviceStatus.Equals("online", StringComparison.OrdinalIgnoreCase);
    public int KindSort => IsActuator ? 0 : 1;
    public string KindLabel => IsActuator ? "Actuator" : "Sensor";
    public string StatusLabel => IsOnline ? "Online" : "Offline";
    public string StatusBrush => IsOnline ? "#16A34A" : "#64748B";
    public string CardBackground => IsOnline ? "#FFFFFF" : "#F8FAFC";
    public string CardBorderBrush => IsOnline ? "#D8E0EA" : "#E2E8F0";
    public string PrimaryBrush => IsOnline ? "#0F172A" : "#94A3B8";
    public string MutedBrush => IsOnline ? "#64748B" : "#94A3B8";
    public string IconBrush => IsOnline ? "#2563EB" : "#94A3B8";
    public string IconBackground => IsOnline ? "#EFF6FF" : "#E2E8F0";
    public string KindIcon => ResolveKindIcon();
    public string KindIconData => ResolveKindIconData();
    public string ValueWithUnit => string.IsNullOrWhiteSpace(Unit) || Value == "No readings yet" ? Value : $"{Value} {Unit}";
    public string PrimaryValue => StatusLabel;
    public string SensorFooterText => Value == "No readings yet" ? "Last value: Waiting" : $"Last value: {ValueWithUnit}";
    public string SecondaryText => !IsOnline ? "Device offline" : IsActuator ? LastStatus : _lastStatus;
    public string SwitchBackground => !IsOnline ? "#CBD5E1" : IsOn ? "#2563EB" : "#CBD5E1";
    public string SwitchKnobAlignment => IsOn ? "Right" : "Left";
    public string SwitchLabel => IsOn ? "On" : "Off";
    public string SwitchLabelBrush => IsOn && IsOnline ? "#2563EB" : "#64748B";

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
                OnPropertyChanged(nameof(PrimaryValue));
                OnPropertyChanged(nameof(SwitchBackground));
                OnPropertyChanged(nameof(SwitchKnobAlignment));
                OnPropertyChanged(nameof(SwitchLabel));
                OnPropertyChanged(nameof(SwitchLabelBrush));
            }
        }
    }

    public string LastStatus
    {
        get => _lastStatus;
        set
        {
            if (SetProperty(ref _lastStatus, value))
            {
                OnPropertyChanged(nameof(SecondaryText));
            }
        }
    }

    public string StateText => IsOn ? "On" : "Off";
    public string ButtonText => IsOn ? "Turn off" : "Turn on";
    public string NextCommandName => IsOn && SupportedCommands.Contains("OFF", StringComparer.OrdinalIgnoreCase) ? "OFF" : "ON";
    public JsonElement NextDesiredValue => DataType == "boolean"
        ? JsonSerializer.SerializeToElement(!IsOn)
        : JsonSerializer.SerializeToElement(IsOn ? "OFF" : "ON");

    public static HomeDeviceCard From(DeviceSummary device, DeviceCapability capability, TelemetryValue? latest)
    {
        var isOn = latest?.BooleanValue == true;
        var value = latest?.DisplayValue ?? "No readings yet";
        var recordedAt = latest?.RecordedAtUtc.ToLocalTime().ToString("g") ?? "Waiting for telemetry";
        return new HomeDeviceCard(
            device.Id,
            device.NameOrSerial,
            device.Status,
            capability.Key,
            capability.DisplayName,
            capability.Kind,
            capability.DataType,
            capability.Unit,
            capability.SupportedCommands,
            value,
            recordedAt,
            isOn);
    }

    public void MarkSubmitted(string commandName, string status)
    {
        if (DataType == "boolean")
        {
            IsOn = !IsOn;
        }

        LastStatus = $"Command {commandName} is {status}.";
    }

    public void UpdateFrom(HomeDeviceCard incoming)
    {
        DeviceName = incoming.DeviceName;
        DeviceStatus = incoming.DeviceStatus;
        SupportedCommands = incoming.SupportedCommands;
        Value = incoming.Value;
        LastStatus = incoming.LastStatus;
        IsOn = incoming.IsOn;
    }

    private string ResolveKindIcon()
    {
        var key = $"{CapabilityKey} {CapabilityName} {Unit}".ToLowerInvariant();
        if (key.Contains("temp"))
        {
            return "\uE7A3";
        }

        if (key.Contains("humid") || key.Contains("water") || key.Contains("pump") || key.Contains("moisture"))
        {
            return "\uE9D0";
        }

        if (key.Contains("light") || key.Contains("lux"))
        {
            return "\uE706";
        }

        if (key.Contains("motion"))
        {
            return "\uE9D9";
        }

        if (key.Contains("fan"))
        {
            return "\uE9CA";
        }

        if (key.Contains("lock"))
        {
            return "\uE72E";
        }

        return IsActuator ? "\uE945" : "\uE95B";
    }

    private string ResolveKindIconData()
    {
        var key = $"{CapabilityKey} {CapabilityName} {Unit}".ToLowerInvariant();
        if (key.Contains("temp"))
        {
            return "M14,4 L14,14.54 A4,4 0 1 1 10,14.54 L10,4 A2,2 0 0 1 14,4 Z";
        }

        if (key.Contains("humid") || key.Contains("water") || key.Contains("moisture"))
        {
            return "M12,22 A7,7 0 0 0 19,15 C19,13 18,11.1 16,9.5 C14,7.9 12.5,5.5 12,3 C11.5,5.5 10,7.9 8,9.5 C6,11.1 5,13 5,15 A7,7 0 0 0 12,22 Z";
        }

        if (key.Contains("pump"))
        {
            return "M2,7 A2,2 0 0 1 4,5 L13,5 A2,2 0 0 1 15,7 L15,17 A2,2 0 0 1 13,19 L4,19 A2,2 0 0 1 2,17 Z M2,8 A4,4 0 0 0 2,16 M5,8 L12,8 M5,11 L12,11 M5,14 L12,14 M5,16 L12,16 M15,8 L18,9 L18,15 L15,16 M18,10 L20,10 L20,14 L18,14 M20,12 L23,12";
        }

        if (key.Contains("light") || key.Contains("lux"))
        {
            return IsActuator
                ? "M15,14 C15.2,13 15.7,12.3 16.5,11.5 C17.5,10.6 18,9.3 18,8 A6,6 0 0 0 6,8 C6,9 6.2,10.2 7.5,11.5 C8.2,12.2 8.8,13 9,14 M9,18 L15,18 M10,22 L14,22"
                : "M12,8 A4,4 0 1 1 11.99,8 M12,4 L12.01,4 M20,12 L20.01,12 M12,20 L12.01,20 M4,12 L4.01,12 M17.657,6.343 L17.667,6.343 M17.657,17.657 L17.667,17.657 M6.343,17.657 L6.353,17.657 M6.343,6.343 L6.353,6.343";
        }

        if (key.Contains("motion") || key.Contains("move"))
        {
            return "M4,16 L4,13.62 C4,11.5 2.97,10.5 3,8 C3.03,5.28 4.49,2 7.5,2 C9.37,2 10,3.8 10,5.5 C10,8.61 8,11.16 8,14.18 L8,16 A2,2 0 1 1 4,16 Z M20,20 L20,17.62 C20,15.5 21.03,14.5 21,12 C20.97,9.28 19.51,6 16.5,6 C14.63,6 14,7.8 14,9.5 C14,12.61 16,15.16 16,18.18 L16,20 A2,2 0 1 0 20,20 Z M16,17 L20,17 M4,13 L8,13";
        }

        if (key.Contains("fan"))
        {
            return "M10.827,16.379 A6.082,6.082 0 0 1 2.209,9.377 L7.621,10.827 A6.082,6.082 0 0 1 14.623,2.209 L13.173,7.621 A6.082,6.082 0 0 1 21.791,14.623 L16.379,13.173 A6.082,6.082 0 0 1 9.377,21.791 Z M12,12 L12,12.01";
        }

        if (key.Contains("door") || key.Contains("lock"))
        {
            return "M10,12 L10.01,12 M18,9 L18,6 A2,2 0 0 0 16,4 L8,4 A2,2 0 0 0 6,6 L6,20 M2,20 L10,20 M20,17 L20,15 A2,2 0 1 0 16,15 L16,17 M15,17 L21,17 A1,1 0 0 1 22,18 L22,21 A1,1 0 0 1 21,22 L15,22 A1,1 0 0 1 14,21 L14,18 A1,1 0 0 1 15,17";
        }

        if (key.Contains("switch") || key.Contains("power"))
        {
            return "M12,2 A10,10 0 1 1 11.99,2 M12,7 L12,11 M7.998,9.003 A5,5 0 1 0 15.998,8.998";
        }

        return IsActuator
            ? "M4,14 A1,1 0 0 1 3.22,12.37 L13.12,2.17 A0.5,0.5 0 0 1 13.98,2.63 L12.06,8.65 A1,1 0 0 0 13,10 L20,10 A1,1 0 0 1 20.78,11.63 L10.88,21.83 A0.5,0.5 0 0 1 10.02,21.37 L11.94,15.35 A1,1 0 0 0 11,14 Z"
            : "M8.464,15.536 C6.512,13.583 6.512,10.417 8.464,8.464 M5.636,18.364 C2.121,14.849 2.121,9.151 5.636,5.636 M15.536,15.536 C17.488,13.583 17.488,10.417 15.536,8.464 M18.364,18.364 C21.879,14.849 21.879,9.151 18.364,5.636 M13,12 C13,12.552 12.552,13 12,13 C11.448,13 11,12.552 11,12 C11,11.448 11.448,11 12,11 C12.552,11 13,11.448 13,12";
    }
}

public sealed class ActuatorControl(
    string key,
    string displayName,
    string dataType,
    IReadOnlyList<string> supportedCommands,
    bool isOn,
    string lastStatus,
    bool isDeviceOnline) : ObservableObject
{
    private string _lastStatus = lastStatus;
    private bool _isOn = isOn;
    private string _displayName = displayName;
    private IReadOnlyList<string> _supportedCommands = supportedCommands;
    private bool _isDeviceOnline = isDeviceOnline;

    public string Key { get; } = key;
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string DataType { get; } = dataType;
    public IReadOnlyList<string> SupportedCommands
    {
        get => _supportedCommands;
        private set
        {
            if (SetProperty(ref _supportedCommands, value))
            {
                OnPropertyChanged(nameof(NextCommandName));
            }
        }
    }

    public bool IsDeviceOnline
    {
        get => _isDeviceOnline;
        private set
        {
            if (SetProperty(ref _isDeviceOnline, value))
            {
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(ContentBrush));
                OnPropertyChanged(nameof(IconBrush));
                OnPropertyChanged(nameof(IconBackground));
                OnPropertyChanged(nameof(SwitchBackground));
            }
        }
    }

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
                OnPropertyChanged(nameof(SwitchBackground));
                OnPropertyChanged(nameof(SwitchKnobAlignment));
            }
        }
    }

    public string StateText => IsOn ? "On" : "Off";
    public string ButtonText => IsOn ? "Turn off" : "Turn on";
    public string CardBackground => IsDeviceOnline ? "#F8FAFC" : "#F1F5F9";
    public string CardBorderBrush => IsDeviceOnline ? "#D8E0EA" : "#E2E8F0";
    public string ContentBrush => IsDeviceOnline ? "#0F172A" : "#94A3B8";
    public string MutedBrush => "#94A3B8";
    public string IconBrush => IsDeviceOnline ? "#2563EB" : "#94A3B8";
    public string IconBackground => IsDeviceOnline ? "#EFF6FF" : "#E2E8F0";
    public string SwitchBackground => !IsDeviceOnline ? "#CBD5E1" : IsOn ? "#2563EB" : "#E2E8F0";
    public HorizontalAlignment SwitchKnobAlignment => IsOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
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

    public void UpdateFrom(ActuatorControl incoming)
    {
        DisplayName = incoming.DisplayName;
        SupportedCommands = incoming.SupportedCommands;
        IsDeviceOnline = incoming.IsDeviceOnline;
        IsOn = incoming.IsOn;
        LastStatus = incoming.LastStatus;
    }
}
