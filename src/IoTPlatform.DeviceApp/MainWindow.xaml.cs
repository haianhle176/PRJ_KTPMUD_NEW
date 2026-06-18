using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IoTPlatform.DeviceApp.Configuration;
using IoTPlatform.DeviceApp.Mqtt;
using IoTPlatform.DeviceApp.Profiles;
using IoTPlatform.DeviceApp.Services;
using IoTPlatform.Domain.Devices;
using IoTPlatform.Domain.Messaging;
using Microsoft.Extensions.Configuration;

namespace IoTPlatform.DeviceApp;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DeviceAppApiClient _api = new();
    private readonly DeviceHiveMqOptions _hiveMqOptions;
    private DeviceTypePreset? _selectedDeviceType;
    private bool _isAuthenticated;
    private string _authEmail = "";
    private string _authStatus = "Đăng nhập bằng cùng tài khoản OmniConnect.";
    private string _signedInDisplayName = "";

    public MainWindow()
    {
        InitializeComponent();
        DeviceTypes = new ObservableCollection<DeviceTypePreset>(DeviceTypePreset.All);
        SelectedDeviceType = DeviceTypes.First();
        _hiveMqOptions = LoadHiveMqOptions();
        DataContext = this;
        _ = RestoreSessionAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceTypePreset> DeviceTypes { get; }
    public ObservableCollection<SimulatedDeviceItem> Devices { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            if (_isAuthenticated != value)
            {
                _isAuthenticated = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AuthVisibility));
                OnPropertyChanged(nameof(SimulatorVisibility));
                OnPropertyChanged(nameof(AppWindowWidth));
                OnPropertyChanged(nameof(AppWindowHeight));
                OnPropertyChanged(nameof(AppWindowMinWidth));
                OnPropertyChanged(nameof(AppWindowMinHeight));
                OnPropertyChanged(nameof(AppWindowMaxWidth));
                OnPropertyChanged(nameof(AppWindowMaxHeight));
            }
        }
    }

    public string AuthEmail
    {
        get => _authEmail;
        set
        {
            if (_authEmail != value)
            {
                _authEmail = value;
                OnPropertyChanged();
            }
        }
    }

    public string AuthStatus
    {
        get => _authStatus;
        set
        {
            if (_authStatus != value)
            {
                _authStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public string SignedInDisplayName
    {
        get => _signedInDisplayName;
        set
        {
            if (_signedInDisplayName != value)
            {
                _signedInDisplayName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SignedInText));
            }
        }
    }

    public string SignedInText => string.IsNullOrWhiteSpace(SignedInDisplayName)
        ? "Signed in"
        : $"Signed in as {SignedInDisplayName}";

    public Visibility AuthVisibility => IsAuthenticated ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SimulatorVisibility => IsAuthenticated ? Visibility.Visible : Visibility.Collapsed;

    public double AppWindowWidth => IsAuthenticated ? 980 : 420;

    public double AppWindowHeight => IsAuthenticated ? 680 : 520;

    public double AppWindowMinWidth => AppWindowWidth;

    public double AppWindowMinHeight => AppWindowHeight;

    public double AppWindowMaxWidth => AppWindowWidth;

    public double AppWindowMaxHeight => AppWindowHeight;

    public DeviceTypePreset? SelectedDeviceType
    {
        get => _selectedDeviceType;
        set
        {
            if (!Equals(_selectedDeviceType, value))
            {
                _selectedDeviceType = value;
                OnPropertyChanged();
            }
        }
    }

    private async void Login_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AuthEmail) || string.IsNullOrWhiteSpace(PasswordInput.Password))
        {
            AuthStatus = "Email và mật khẩu là bắt buộc.";
            return;
        }

        try
        {
            AuthStatus = "Đang đăng nhập...";
            var user = await _api.LoginAsync(AuthEmail.Trim(), PasswordInput.Password);
            SignedInDisplayName = user.DisplayName;
            IsAuthenticated = true;
            PasswordInput.Clear();
            Log($"Signed in as {user.Email}.");
        }
        catch (Exception exception)
        {
            AuthStatus = exception.Message;
        }
    }

    private async void Logout_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in Devices.ToArray())
        {
            await StopDeviceAsync(item);
        }

        Devices.Clear();
        await _api.LogoutAsync();
        SignedInDisplayName = "";
        IsAuthenticated = false;
        AuthStatus = "Đăng nhập bằng cùng tài khoản OmniConnect.";
        Log("Signed out.");
    }

    private async void AddDevice_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SelectedDeviceType is null)
            {
                return;
            }

            var profile = new DeviceProfile(
                DeviceSerial.Create(),
                1,
                SelectedDeviceType.Capabilities,
                SelectedDeviceType.DeviceType);

            await _api.ProvisionDeviceAsync(
                profile.Serial,
                SelectedDeviceType.DisplayName,
                SelectedDeviceType.Group,
                profile.DeviceType,
                profile.ManifestVersion,
                profile.Capabilities);

            Devices.Add(new SimulatedDeviceItem(profile, SelectedDeviceType));
            Log($"Added {SelectedDeviceType.DisplayName} to your OmniConnect account.");
        }
        catch (Exception exception)
        {
            App.WriteCrashLog(exception);
            Log($"Could not add device: {exception.Message}");
        }
    }

    private async void StartDevice_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SimulatedDeviceItem item)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_hiveMqOptions.Host) ||
            string.IsNullOrWhiteSpace(_hiveMqOptions.Username) ||
            string.IsNullOrWhiteSpace(_hiveMqOptions.Password))
        {
            Log("HiveMQ settings are missing. Configure user-secrets or environment variables.");
            return;
        }

        try
        {
            item.ApplySettings();
            item.Session ??= new SimulatedDeviceSession(_hiveMqOptions, item.Profile, Log, item.CreateInitialActuatorStates());
            item.Status = "Connecting";
            await item.Session.StartAsync();
            item.PairingCode = item.Session.PairingCode;
            item.Status = "Online";
        }
        catch (Exception exception)
        {
            App.WriteCrashLog(exception);
            item.Status = "Error";
            Log($"Could not start {item.Serial}: {exception.Message}");
            if (item.Session is not null)
            {
                await item.Session.StopAsync();
                item.Session = null;
            }
        }
    }

    private async void CopyPairingCode_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SimulatedDeviceItem item || string.IsNullOrWhiteSpace(item.PairingCode) || item.PairingCode == "-")
        {
            Log("Start the device before copying its pairing code.");
            return;
        }

        var copyError = await TryCopyToClipboardAsync(item.PairingCode);
        if (copyError is null)
        {
            Log($"Copied pairing code for {item.Serial}.");
            return;
        }

        Log($"Clipboard copy failed ({copyError}). Type this code manually: {item.PairingCode}");
    }

    private static async Task<string?> TryCopyToClipboardAsync(string text)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return null;
            }
            catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or System.Runtime.InteropServices.ExternalException or ThreadStateException)
            {
                lastException = exception;
                if (attempt < 10)
                {
                    await Task.Delay(75);
                }
            }
        }

        return lastException is null
            ? "unknown error"
            : $"{lastException.GetType().Name}: {lastException.Message}";
    }

    private async void StopDevice_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SimulatedDeviceItem item)
        {
            await StopDeviceAsync(item);
        }
    }

    private async void RemoveDevice_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SimulatedDeviceItem item)
        {
            await StopDeviceAsync(item);
            Devices.Remove(item);
            Log($"Removed {item.Serial}.");
        }
    }

    private async void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        foreach (var item in Devices.ToArray())
        {
            await StopDeviceAsync(item);
        }
    }

    private async Task StopDeviceAsync(SimulatedDeviceItem item)
    {
        if (item.Session is null)
        {
            item.Status = "Stopped";
            return;
        }

        item.Status = "Stopping";
        await item.Session.StopAsync();
        item.Status = "Stopped";
        item.Session = null;
    }

    private void Log(string message)
    {
        Dispatcher.Invoke(() =>
        {
            Logs.Insert(0, $"{DateTime.Now:T}  {message}");
            while (Logs.Count > 200)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        });
    }

    private async Task RestoreSessionAsync()
    {
        if (await _api.TryRestoreAsync())
        {
            IsAuthenticated = true;
            SignedInDisplayName = "";
            Log("Restored signed-in session.");
            Log("Ready. Add one or more simulated devices.");
            return;
        }

        IsAuthenticated = false;
    }

    private static DeviceHiveMqOptions LoadHiveMqOptions()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(typeof(MainWindow).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DeviceHiveMqOptions();
        configuration.GetSection(DeviceHiveMqOptions.SectionName).Bind(options);
        return options;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class SimulatedDeviceItem(DeviceProfile profile, DeviceTypePreset preset) : INotifyPropertyChanged
{
    private string _status = "Stopped";
    private string _pairingCode = "-";
    private string _sensorMinValue = profile.Capabilities.FirstOrDefault()?.MinValue?.ToString() ?? "";
    private string _sensorMaxValue = profile.Capabilities.FirstOrDefault()?.MaxValue?.ToString() ?? "";
    private bool _actuatorValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceProfile Profile { get; private set; } = profile;
    public SimulatedDeviceSession? Session { get; set; }
    public string Serial => Profile.Serial;
    public string DisplayName => preset.DisplayName;
    public string Icon => preset.Icon;
    public string IconData => preset.IconData;
    public Visibility SensorSettingsVisibility => IsNumericSensor ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActuatorSettingsVisibility => IsActuator ? Visibility.Visible : Visibility.Collapsed;
    public string ActuatorValueText => ActuatorValue ? "On / true" : "Off / false";

    private bool IsNumericSensor => preset.Group.Equals("sensor", StringComparison.OrdinalIgnoreCase) &&
        preset.Capabilities.Any(capability => capability.DataType.Equals("number", StringComparison.OrdinalIgnoreCase));

    private bool IsActuator => preset.Group.Equals("actuator", StringComparison.OrdinalIgnoreCase);

    public string SensorMinValue
    {
        get => _sensorMinValue;
        set
        {
            if (_sensorMinValue != value)
            {
                _sensorMinValue = value;
                OnPropertyChanged();
            }
        }
    }

    public string SensorMaxValue
    {
        get => _sensorMaxValue;
        set
        {
            if (_sensorMaxValue != value)
            {
                _sensorMaxValue = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ActuatorValue
    {
        get => _actuatorValue;
        set
        {
            if (_actuatorValue != value)
            {
                _actuatorValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActuatorValueText));
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusForeground));
            }
        }
    }

    public Brush StatusBackground => Status switch
    {
        "Online" => new SolidColorBrush(Color.FromRgb(220, 252, 231)),
        "Connecting" or "Stopping" => new SolidColorBrush(Color.FromRgb(254, 249, 195)),
        _ => new SolidColorBrush(Color.FromRgb(241, 245, 249))
    };

    public Brush StatusForeground => Status switch
    {
        "Online" => new SolidColorBrush(Color.FromRgb(21, 128, 61)),
        "Connecting" or "Stopping" => new SolidColorBrush(Color.FromRgb(161, 98, 7)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
    };

    public string PairingCode
    {
        get => _pairingCode;
        set
        {
            if (_pairingCode != value)
            {
                _pairingCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PairingCodeText));
                OnPropertyChanged(nameof(PairingCodeDisplay));
            }
        }
    }

    public string PairingCodeText => PairingCode == "-" ? "Pairing code appears after Start" : $"Pairing code: {PairingCode}";

    public string PairingCodeDisplay => PairingCode == "-" ? "Pairing code appears after Start" : PairingCode;

    public void ApplySettings()
    {
        if (!IsNumericSensor)
        {
            return;
        }

        var minValue = ParseDecimalOrDefault(SensorMinValue, Profile.Capabilities[0].MinValue);
        var maxValue = ParseDecimalOrDefault(SensorMaxValue, Profile.Capabilities[0].MaxValue);
        if (minValue is not null && maxValue is not null && maxValue < minValue)
        {
            (minValue, maxValue) = (maxValue, minValue);
            SensorMinValue = minValue.Value.ToString();
            SensorMaxValue = maxValue.Value.ToString();
        }

        Profile = Profile with
        {
            ManifestVersion = Profile.ManifestVersion + 1,
            Capabilities = Profile.Capabilities
                .Select(capability => capability with { MinValue = minValue, MaxValue = maxValue })
                .ToArray()
        };
    }

    public IReadOnlyDictionary<string, JsonElement> CreateInitialActuatorStates()
    {
        if (!IsActuator)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        return Profile.Capabilities
            .Where(capability => capability.IsWritable && capability.DataType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                capability => capability.Key,
                _ => JsonSerializer.SerializeToElement(ActuatorValue, DeviceMessageSerializer.Options),
                StringComparer.Ordinal);
    }

    private static decimal? ParseDecimalOrDefault(string value, decimal? fallback) =>
        decimal.TryParse(value, out var result) ? result : fallback;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
