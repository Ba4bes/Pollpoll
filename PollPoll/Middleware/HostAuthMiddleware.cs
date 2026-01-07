namespace PollPoll.Middleware;

/// <summary>
/// Middleware to authenticate host requests using a simple token mechanism.
/// Validates X-Host-Token header or HostAuth cookie against configured token.
/// </summary>
public class HostAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HostAuthMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostAuthMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public HostAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<HostAuthMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to validate host authentication.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Only apply to /host/* endpoints
        if (path.StartsWith("/host", StringComparison.OrdinalIgnoreCase))
        {
            var expectedToken = _configuration["HostAuth:Token"];

            if (string.IsNullOrWhiteSpace(expectedToken))
            {
                _logger.LogWarning("HostAuth:Token not configured in appsettings.json");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Host authentication not configured.");
                return;
            }

            // Check X-Host-Token header first
            var headerToken = context.Request.Headers["X-Host-Token"].FirstOrDefault();

            // Check HostAuth cookie as fallback
            var cookieToken = context.Request.Cookies["HostAuth"];

            var providedToken = headerToken ?? cookieToken;

            if (string.IsNullOrWhiteSpace(providedToken) || providedToken != expectedToken)
            {
                _logger.LogWarning("Unauthorized host access attempt to {Path}", path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized. Valid host token required.");
                return;
            }

            // Authentication successful - set cookie if not already present
            if (string.IsNullOrWhiteSpace(cookieToken))
            {
                context.Response.Cookies.Append("HostAuth", expectedToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering HostAuthMiddleware.
/// </summary>
public static class HostAuthMiddlewareExtensions
{
    /// <summary>
    /// Adds host authentication middleware to the pipeline.
    /// </summary>
    /// <param name="builder">Application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseHostAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<HostAuthMiddleware>();
    }
}
