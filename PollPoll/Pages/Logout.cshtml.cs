using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PollPoll.Pages;

public class LogoutModel : PageModel
{
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(ILogger<LogoutModel> logger)
    {
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        // Delete the HostAuth cookie
        Response.Cookies.Delete("HostAuth");

        _logger.LogInformation("Host logged out");

        // Redirect to login page
        return RedirectToPage("/Login");
    }
}
