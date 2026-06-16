using IoTPlatform.DeviceApp.Configuration;
using IoTPlatform.DeviceApp.Mqtt;
using IoTPlatform.DeviceApp.Profiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddOptions<DeviceHiveMqOptions>()
    .Bind(builder.Configuration.GetSection(DeviceHiveMqOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "HiveMq:Host is required.")
    .Validate(options => options.Port is > 0 and <= 65535, "HiveMq:Port must be valid.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "HiveMq:Username is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "HiveMq:Password is required.")
    .ValidateOnStart();
builder.Services.AddOptions<DeviceAppOptions>()
    .Bind(builder.Configuration.GetSection(DeviceAppOptions.SectionName))
    .Validate(options => options.ReconnectMinSeconds > 0, "DeviceApp:ReconnectMinSeconds must be positive.")
    .Validate(options => options.ReconnectMaxSeconds >= options.ReconnectMinSeconds, "DeviceApp reconnect range is invalid.")
    .ValidateOnStart();
builder.Services.AddSingleton<DeviceMqttClient>();

using var host = builder.Build();
var options = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DeviceAppOptions>>().Value;
var profileName = GetArgument(args, "--profile") ?? "default";
var reconfigure = args.Any(argument => string.Equals(argument, "--reconfigure", StringComparison.OrdinalIgnoreCase));
var rootDirectory = string.IsNullOrWhiteSpace(options.DataDirectory)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniConnect", "DeviceApp")
    : options.DataDirectory;
var profile = await new DeviceProfileStore(rootDirectory).LoadOrCreateAsync(
    profileName,
    InteractiveManifestBuilder.CreateAsync,
    reconfigure);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

Console.WriteLine($"Profile: {profileName}");
Console.WriteLine($"Serial: {profile.Serial}");
Console.WriteLine($"Capabilities: {string.Join(", ", profile.Capabilities.Select(capability => capability.Key))}");
await host.Services.GetRequiredService<DeviceMqttClient>().RunAsync(profile, shutdown.Token);

static string? GetArgument(string[] arguments, string name)
{
    var index = Array.FindIndex(arguments, argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

public partial class Program;
