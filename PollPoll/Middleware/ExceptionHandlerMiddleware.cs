using System.Net;

namespace PollPoll.Middleware;

/// <summary>
/// Global exception handler middleware that catches unhandled exceptions
/// and returns user-friendly error messages.
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlerMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="environment">Web host environment.</param>
    public ExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlerMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Invokes the middleware to handle exceptions.
    /// </summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var userMessage = GetUserFriendlyMessage(exception);
        var technicalDetails = _environment.IsDevelopment()
            ? $"<details><summary>Technical Details (Development Only)</summary><pre>{exception}</pre></details>"
            : string.Empty;

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Error - PollPoll</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 600px;
            margin: 100px auto;
            padding: 20px;
            text-align: center;
        }}
        h1 {{
            color: #d32f2f;
            font-size: 24px;
        }}
        p {{
            color: #555;
            font-size: 16px;
            line-height: 1.5;
        }}
        .action {{
            margin-top: 30px;
        }}
        a {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #1976d2;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            font-weight: 500;
        }}
        a:hover {{
            background-color: #1565c0;
        }}
        details {{
            margin-top: 30px;
            text-align: left;
            background: #f5f5f5;
            padding: 15px;
            border-radius: 4px;
        }}
        pre {{
            white-space: pre-wrap;
            word-wrap: break-word;
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <h1>Something went wrong</h1>
    <p>{userMessage}</p>
    <div class='action'>
        <a href='javascript:history.back()'>Go Back</a>
        <a href='/'>Home</a>
    </div>
    {technicalDetails}
</body>
</html>";

        await context.Response.WriteAsync(html);
    }

    private static string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            ArgumentException => "Invalid input provided. Please check your data and try again.",
            InvalidOperationException => "This action cannot be completed right now. Please try again.",
            UnauthorizedAccessException => "You don't have permission to perform this action.",
            KeyNotFoundException => "The requested resource was not found.",
            _ => "An unexpected error occurred. Please try again later or contact support if the problem persists."
        };
    }
}

/// <summary>
/// Extension methods for registering ExceptionHandlerMiddleware.
/// </summary>
public static class ExceptionHandlerMiddlewareExtensions
{
    /// <summary>
    /// Adds global exception handler middleware to the pipeline.
    /// </summary>
    /// <param name="builder">Application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlerMiddleware>();
    }
}
