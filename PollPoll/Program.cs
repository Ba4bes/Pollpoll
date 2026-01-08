using Microsoft.EntityFrameworkCore;
using PollPoll.Data;
using PollPoll.Hubs;
using PollPoll.Middleware;
using PollPoll.Services;
using PollPoll.Filters;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault integration
if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = new Uri("https://kv-pollpoll.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential());
}

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddRazorPages();

// Add Controllers for API endpoints
builder.Services.AddControllers();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add HttpContextAccessor for VoteService cookie management
builder.Services.AddHttpContextAccessor();

// Add Memory Cache for QR code caching
builder.Services.AddMemoryCache();

// Register application services
builder.Services.AddScoped<PollService>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddScoped<ResultsService>();
builder.Services.AddScoped<QRCodeService>();

// Add DbContext with SQLite
builder.Services.AddDbContext<PollDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PollDb")));

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PollDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Ensuring database directory exists and applying migrations...");
        
        // Ensure the database directory exists
        var connectionString = builder.Configuration.GetConnectionString("PollDb");
        if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Data Source="))
        {
            var dbPath = connectionString.Split("Data Source=")[1].Split(';')[0];
            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                logger.LogInformation("Created database directory: {Directory}", dbDirectory);
            }
        }
        
        // Apply migrations
        dbContext.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

// Configure the HTTP request pipeline.

// Performance monitoring (T107 - logs request durations and PERF violations)
app.UsePerformanceMonitoring();

// Global exception handler (must be after performance monitoring)
app.UseGlobalExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Static files
app.MapStaticAssets();

// Routing
app.UseRouting();

// Host authentication middleware
app.UseHostAuth();

// Authorization
app.UseAuthorization();

// Map endpoints
app.MapControllers();
app.MapRazorPages()
   .WithStaticAssets();

// Map SignalR hubs
app.MapHub<ResultsHub>("/hubs/results");

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
