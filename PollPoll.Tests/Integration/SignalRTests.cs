using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PollPoll.Data;
using PollPoll.Models;
using System.Data.Common;

namespace PollPoll.Tests.Integration;

public class SignalRTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly DbConnection _connection;

    public SignalRTests(WebApplicationFactory<Program> factory)
    {
        // Create persistent SQLite in-memory connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Enable WAL mode for better concurrency
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PollDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add DbContext using the shared in-memory connection
                services.AddDbContext<PollDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Ensure database is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PollDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task ResultsHub_VoteUpdated_BroadcastsToAllClients()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "TEST",
            Question = "Test poll",
            ChoiceMode = ChoiceMode.Single
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        var option = new Option { PollId = poll.Id, Text = "Option A", DisplayOrder = 0 };
        context.Options.Add(option);
        await context.SaveChangesAsync();

        // Create SignalR connection
        var client = _factory.CreateClient();
        var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');
        
        var connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/results", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        // Setup listener for VoteUpdated event
        var tcs = new TaskCompletionSource<string>();
        connection.On<string>("VoteUpdated", (pollCode) =>
        {
            tcs.SetResult(pollCode);
        });

        await connection.StartAsync();

        // Join the poll group
        await connection.InvokeAsync("JoinPollGroup", "TEST");

        // Act - Invoke VoteUpdated from server
        await connection.InvokeAsync("VoteUpdated", "TEST");

        // Assert - Verify broadcast was received
        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().Be("TEST");

        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task ResultsHub_JoinPollGroup_AllowsMultipleClients()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "MULTI",
            Question = "Multi-client test",
            ChoiceMode = ChoiceMode.Single
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        var option = new Option { PollId = poll.Id, Text = "Option", DisplayOrder = 0 };
        context.Options.Add(option);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');

        // Create two separate SignalR connections
        var connection1 = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/results", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/results", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var tcs1 = new TaskCompletionSource<string>();
        var tcs2 = new TaskCompletionSource<string>();

        connection1.On<string>("VoteUpdated", (pollCode) => tcs1.SetResult(pollCode));
        connection2.On<string>("VoteUpdated", (pollCode) => tcs2.SetResult(pollCode));

        await connection1.StartAsync();
        await connection2.StartAsync();

        // Both join the same poll group
        await connection1.InvokeAsync("JoinPollGroup", "MULTI");
        await connection2.InvokeAsync("JoinPollGroup", "MULTI");

        // Act - Trigger update from one connection
        await connection1.InvokeAsync("VoteUpdated", "MULTI");

        // Assert - Both connections should receive the update
        var result1 = await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var result2 = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result1.Should().Be("MULTI");
        result2.Should().Be("MULTI");

        await connection1.StopAsync();
        await connection2.StopAsync();
        await connection1.DisposeAsync();
        await connection2.DisposeAsync();
    }

    [Fact]
    public async Task ResultsHub_DifferentGroups_IsolatesBroadcasts()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll1 = new Poll { Code = "AAA", Question = "Poll 1", ChoiceMode = ChoiceMode.Single };
        var poll2 = new Poll { Code = "BBB", Question = "Poll 2", ChoiceMode = ChoiceMode.Single };
        context.Polls.AddRange(poll1, poll2);
        await context.SaveChangesAsync();

        var option1 = new Option { PollId = poll1.Id, Text = "Option A", DisplayOrder = 0 };
        var option2 = new Option { PollId = poll2.Id, Text = "Option B", DisplayOrder = 0 };
        context.Options.AddRange(option1, option2);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');

        var connection1 = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/results", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/results", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var tcs1 = new TaskCompletionSource<string>();
        var tcs2 = new TaskCompletionSource<string>();
        var receivedWrongGroup = false;

        connection1.On<string>("VoteUpdated", (pollCode) =>
        {
            if (pollCode == "BBB") receivedWrongGroup = true;
            tcs1.TrySetResult(pollCode);
        });

        connection2.On<string>("VoteUpdated", (pollCode) => tcs2.SetResult(pollCode));

        await connection1.StartAsync();
        await connection2.StartAsync();

        // Connection 1 joins AAA, Connection 2 joins BBB
        await connection1.InvokeAsync("JoinPollGroup", "AAA");
        await connection2.InvokeAsync("JoinPollGroup", "BBB");

        // Act - Update BBB only
        await connection2.InvokeAsync("VoteUpdated", "BBB");

        // Assert
        var result2 = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result2.Should().Be("BBB");

        // Connection 1 should NOT receive the BBB update
        await Task.Delay(500); // Give it time to potentially receive (it shouldn't)
        receivedWrongGroup.Should().BeFalse();
        tcs1.Task.IsCompleted.Should().BeFalse();

        await connection1.StopAsync();
        await connection2.StopAsync();
        await connection1.DisposeAsync();
        await connection2.DisposeAsync();
    }

    [Fact]
    public async Task ResultsHub_ClientDisconnect_DoesNotAffectOtherClients()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll = new Poll
        {
            Code = "DISC",
            Question = "Disconnect test",
            ChoiceMode = ChoiceMode.Single
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        var option = new Option { PollId = poll.Id, Text = "Option", DisplayOrder = 0 };
        context.Options.Add(option);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');

        var connection1 = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/results", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/results", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var tcs2 = new TaskCompletionSource<string>();
        connection2.On<string>("VoteUpdated", (pollCode) => tcs2.SetResult(pollCode));

        await connection1.StartAsync();
        await connection2.StartAsync();

        await connection1.InvokeAsync("JoinPollGroup", "DISC");
        await connection2.InvokeAsync("JoinPollGroup", "DISC");

        // Act - Disconnect connection1
        await connection1.StopAsync();
        await connection1.DisposeAsync();

        // Broadcast update
        await connection2.InvokeAsync("VoteUpdated", "DISC");

        // Assert - Connection2 should still receive updates
        var result = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().Be("DISC");

        await connection2.StopAsync();
        await connection2.DisposeAsync();
    }
}
