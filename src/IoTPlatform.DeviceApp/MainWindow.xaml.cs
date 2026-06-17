using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using IoTPlatform.DeviceApp.Configuration;
using IoTPlatform.DeviceApp.Mqtt;
using IoTPlatform.DeviceApp.Profiles;
using IoTPlatform.Domain.Devices;
using IoTPlatform.Domain.Messaging;
using Microsoft.Extensions.Configuration;

namespace IoTPlatform.DeviceApp;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DeviceHiveMqOptions _hiveMqOptions;
    private DeviceTypePreset? _selectedDeviceType;

    public MainWindow()
    {
        InitializeComponent();
        DeviceTypes = new ObservableCollection<DeviceTypePreset>(DeviceTypePreset.All);
        SelectedDeviceType = DeviceTypes.First();
        _hiveMqOptions = LoadHiveMqOptions();
        DataContext = this;
        Log("Ready. Add one or more simulated devices.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceTypePreset> DeviceTypes { get; }
    public ObservableCollection<SimulatedDeviceItem> Devices { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];

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

    private void AddDevice_OnClick(object sender, RoutedEventArgs e)
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
        Devices.Add(new SimulatedDeviceItem(profile, SelectedDeviceType));
        Log($"Added {SelectedDeviceType.DisplayName}.");
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

        item.ApplySettings();
        item.Session ??= new SimulatedDeviceSession(_hiveMqOptions, item.Profile, Log, item.CreateInitialActuatorStates());
        item.Status = "Connecting";
        await item.Session.StartAsync();
        item.PairingCode = item.Session.PairingCode;
        item.Status = "Online";
    }

    private void CopyPairingCode_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not SimulatedDeviceItem item || string.IsNullOrWhiteSpace(item.PairingCode) || item.PairingCode == "-")
        {
            Log("Start the device before copying its pairing code.");
            return;
        }

        Clipboard.SetText(item.PairingCode);
        Log($"Copied pairing code for {item.Serial}.");
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

    private static DeviceHiveMqOptions LoadHiveMqOptions()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
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
            }
        }
    }

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
            }
        }
    }

    public string PairingCodeText => PairingCode == "-" ? "Pairing code appears after Start" : $"Pairing code: {PairingCode}";

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
