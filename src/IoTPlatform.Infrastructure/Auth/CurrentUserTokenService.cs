using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IoTPlatform.Application.Security;
using IoTPlatform.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IoTPlatform.Infrastructure.Auth;

public sealed class CurrentUserTokenService(IOptions<JwtOptions> options) : ICurrentUserTokenService
{
    private readonly JwtOptions _options = options.Value;

    public Guid GetUserIdFromBearerToken(string authorizationHeader)
    {
        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Missing bearer token.");
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsed))
        {
            throw new UnauthorizedAccessException("Token does not contain a valid subject.");
        }

        return parsed;
    }
}
