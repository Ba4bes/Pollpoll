using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PollPoll.Filters;

/// <summary>
/// Base page model class that enforces host authentication.
/// Pages that inherit from this class will require a valid host token to access.
/// </summary>
public abstract class AuthenticatedPageModel : PageModel
{
    protected readonly IConfiguration Configuration;
    protected readonly ILogger Logger;

    protected AuthenticatedPageModel(IConfiguration configuration, ILogger logger)
    {
        Configuration = configuration;
        Logger = logger;
    }

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        var expectedToken = Configuration["HostAuth:Token"];

        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            Logger.LogWarning("HostAuth:Token not configured in appsettings.json");
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Check X-Host-Token header first
        var headerToken = HttpContext.Request.Headers["X-Host-Token"].FirstOrDefault();

        // Check HostAuth cookie as fallback
        var cookieToken = HttpContext.Request.Cookies["HostAuth"];

        var providedToken = headerToken ?? cookieToken;

        if (string.IsNullOrWhiteSpace(providedToken) || providedToken != expectedToken)
        {
            Logger.LogWarning("Unauthorized access attempt to protected page: {Path}", HttpContext.Request.Path);

            // Redirect to login page with return URL
            var returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
            context.Result = new RedirectResult($"/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        // Authentication successful - set cookie if not already present
        if (string.IsNullOrWhiteSpace(cookieToken))
        {
            HttpContext.Response.Cookies.Append("HostAuth", expectedToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
        }

        base.OnPageHandlerExecuting(context);
    }
}
