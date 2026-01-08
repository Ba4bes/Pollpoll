using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PollPoll.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IConfiguration configuration, ILogger<LoginModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public string HostToken { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";

        // If already authenticated, redirect to return URL
        var cookieToken = Request.Cookies["HostAuth"];
        var expectedToken = _configuration["HostAuth:Token"];

        if (!string.IsNullOrWhiteSpace(cookieToken) && cookieToken == expectedToken)
        {
            Response.Redirect(ReturnUrl);
        }
    }

    public IActionResult OnPost(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";

        var expectedToken = _configuration["HostAuth:Token"];

        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            _logger.LogError("HostAuth:Token not configured in appsettings.json");
            ErrorMessage = "Host authentication is not configured. Please contact your administrator.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(HostToken))
        {
            ErrorMessage = "Please enter your host token.";
            return Page();
        }

        if (HostToken != expectedToken)
        {
            _logger.LogWarning("Failed login attempt with invalid token");
            ErrorMessage = "Invalid host token. Please try again.";
            HostToken = string.Empty; // Clear the field
            return Page();
        }

        // Authentication successful - set cookie
        Response.Cookies.Append("HostAuth", expectedToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        _logger.LogInformation("Host successfully authenticated");

        return Redirect(ReturnUrl);
    }
}
