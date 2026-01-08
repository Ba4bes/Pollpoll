using Microsoft.EntityFrameworkCore;
using PollPoll.BackgroundServices;
using PollPoll.Data;
using PollPoll.Hubs;
using PollPoll.Middleware;
using PollPoll.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<QRCodeService>();

// Add DbContext with SQLite
builder.Services.AddDbContext<PollDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PollDb")));

var app = builder.Build();

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
