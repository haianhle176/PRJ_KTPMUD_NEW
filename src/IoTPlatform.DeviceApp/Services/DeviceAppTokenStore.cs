using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IoTPlatform.DeviceApp.Services;

public sealed class DeviceAppTokenStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniConnect",
        "DeviceApp",
        "tokens.dat");

    public async Task SaveAsync(DeviceAppAuthTokens tokens)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(tokens);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_path, protectedBytes);
    }

    public async Task<DeviceAppAuthTokens?> LoadAsync()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_path);
            var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
            return JsonSerializer.Deserialize<DeviceAppAuthTokens>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}

public sealed record DeviceAppAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc);
