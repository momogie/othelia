namespace Othelia.Api.Services;

public sealed class AuthOptions
{
    public List<AuthUser> Users { get; set; } = new();
    public int CookieExpireHours { get; set; } = 12;
}

public sealed class AuthUser
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}
