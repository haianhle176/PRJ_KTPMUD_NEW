using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IoTPlatform.Wpf.Services;

public sealed class TokenStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniConnect",
        "Wpf",
        "tokens.dat");

    public async Task SaveAsync(AuthTokens tokens)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(tokens);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_path, protectedBytes);
    }

    public async Task<AuthTokens?> LoadAsync()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_path);
            var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
            return JsonSerializer.Deserialize<AuthTokens>(json);
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

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, DateTimeOffset RefreshTokenExpiresAtUtc);
