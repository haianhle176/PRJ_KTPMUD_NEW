namespace IoTPlatform.Application.Auth;

public abstract class AuthException(string message) : Exception(message);

public sealed class DuplicateEmailException(string email)
    : AuthException($"A user with email '{email}' already exists.");

public sealed class InvalidCredentialsException()
    : AuthException("Invalid email or password.");

public sealed class InvalidRefreshTokenException()
    : AuthException("Refresh token is invalid, expired, or revoked.");
