using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Othelia.Api.Services;

namespace Othelia.Api.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly IAuthService _auth;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IAuthService auth, ILogger<LoginModel> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = _auth.Validate(Username, Password);
        if (user is null)
        {
            Error = "Invalid username or password.";
            return Page();
        }

        var principal = _auth.CreatePrincipal(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });

        _logger.LogInformation("User {Username} signed in.", Username);
        return Redirect("/");
    }
}
