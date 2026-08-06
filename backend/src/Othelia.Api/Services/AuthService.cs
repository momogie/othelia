using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Othelia.Api.Services;

public interface IAuthService
{
    AuthUser? Validate(string username, string password);
    ClaimsPrincipal CreatePrincipal(AuthUser user);
}

public sealed class AuthService : IAuthService
{
    private readonly IOptions<AuthOptions> _options;

    public AuthService(IOptions<AuthOptions> options) => _options = options;

    public AuthUser? Validate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        return _options.Value.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)
            && u.Password == password);
    }

    public ClaimsPrincipal CreatePrincipal(AuthUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Username),
            new(ClaimTypes.Name, user.Username),
            new("displayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName),
            new(ClaimTypes.Role, user.Role),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
