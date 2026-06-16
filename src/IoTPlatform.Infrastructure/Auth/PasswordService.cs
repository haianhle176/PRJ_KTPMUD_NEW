using IoTPlatform.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace IoTPlatform.Infrastructure.Auth;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object User = new();

    public string HashPassword(string password) => _hasher.HashPassword(User, password);

    public bool VerifyPassword(string passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(User, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
