using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PollPoll.Data;
using PollPoll.Models;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PollPoll.Tests.Integration;

/// <summary>
/// Integration tests for User Story 5: Multiple Active Polls
/// Tests that multiple polls can exist simultaneously without auto-closing
/// </summary>
public class MultiActivePollsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MultiActivePollsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all background services (conflicts with in-memory DB in tests)
                var backgroundServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
                foreach (var service in backgroundServices)
                {
                    services.Remove(service);
                }

                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PollDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<PollDbContext>(options =>
                {
                    options.UseInMemoryDatabase("MultiActivePollsTests_" + Guid.NewGuid());
                });
            });
        });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Host-Token", "test-host-token");
    }

    [Fact]
    public async Task CreatePoll_ShouldAllowMultipleActivePolls()
    {
        // Arrange & Act - Create first poll
        var poll1Request = new
        {
            question = "First poll?",
            choiceMode = "Single",
            options = new[] {
                new { text = "A" },
                new { text = "B" }
            }
        };
        var response1 = await _client.PostAsJsonAsync("/host/polls", poll1Request);
        response1.EnsureSuccessStatusCode();
        var poll1 = await response1.Content.ReadFromJsonAsync<CreatePollResponse>();

        // Act - Create second poll (should NOT auto-close first poll)
        var poll2Request = new
        {
            question = "Second poll?",
            choiceMode = "Single",
            options = new[] {
                new { text = "X" },
                new { text = "Y" }
            }
        };
        var response2 = await _client.PostAsJsonAsync("/host/polls", poll2Request);
        response2.EnsureSuccessStatusCode();
        var poll2 = await response2.Content.ReadFromJsonAsync<CreatePollResponse>();

        // Assert - Both polls should exist and be open
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var allPolls = await context.Polls.ToListAsync();
        var openPolls = allPolls.Where(p => !p.IsClosed).ToList();

        openPolls.Should().HaveCount(2, "both polls should remain open");
        openPolls.Should().Contain(p => p.Code == poll1!.Code);
        openPolls.Should().Contain(p => p.Code == poll2!.Code);
    }

    [Fact]
    public async Task VoteOnMultiplePolls_ShouldWork()
    {
        // Arrange - Create two polls
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll1 = new Poll { Code = "AAA1", Question = "Q1", ChoiceMode = ChoiceMode.Single, IsClosed = false, CreatedAt = DateTime.UtcNow };
        var option1 = new Option { PollId = poll1.Id, Text = "Option 1", DisplayOrder = 0 };
        poll1.Options.Add(option1);

        var poll2 = new Poll { Code = "BBB2", Question = "Q2", ChoiceMode = ChoiceMode.Single, IsClosed = false, CreatedAt = DateTime.UtcNow };
        var option2 = new Option { PollId = poll2.Id, Text = "Option 2", DisplayOrder = 0 };
        poll2.Options.Add(option2);

        context.Polls.AddRange(poll1, poll2);
        await context.SaveChangesAsync();

        // Act - Vote on both polls
        var voteClient = _factory.CreateClient(); // New client without host token
        var vote1Response = await voteClient.PostAsJsonAsync($"/p/{poll1.Code}/vote", new { selectedOptionId = option1.Id });
        var vote2Response = await voteClient.PostAsJsonAsync($"/p/{poll2.Code}/vote", new { selectedOptionId = option2.Id });

        // Assert
        vote1Response.StatusCode.Should().Be(HttpStatusCode.OK, "vote on first poll should succeed");
        vote2Response.StatusCode.Should().Be(HttpStatusCode.OK, "vote on second poll should succeed");

        var votes = await context.Votes.ToListAsync();
        votes.Should().HaveCount(2, "both votes should be recorded");
        votes.Should().Contain(v => v.PollId == poll1.Id);
        votes.Should().Contain(v => v.PollId == poll2.Id);
    }

    [Fact]
    public async Task ClosePoll_ShouldOnlyCloseTargetPoll()
    {
        // Arrange - Create two polls
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();

        var poll1 = new Poll { Code = "CLS1", Question = "To close", ChoiceMode = ChoiceMode.Single, IsClosed = false, CreatedAt = DateTime.UtcNow };
        var poll2 = new Poll { Code = "OPN2", Question = "Stay open", ChoiceMode = ChoiceMode.Single, IsClosed = false, CreatedAt = DateTime.UtcNow };
        context.Polls.AddRange(poll1, poll2);
        await context.SaveChangesAsync();

        // Act - Close only poll1
        var response = await _client.PostAsync($"/host/polls/{poll1.Code}/close", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var poll1AfterClose = await context.Polls.FirstOrDefaultAsync(p => p.Code == "CLS1");
        var poll2AfterClose = await context.Polls.FirstOrDefaultAsync(p => p.Code == "OPN2");

        poll1AfterClose!.IsClosed.Should().BeTrue("poll1 should be closed");
        poll2AfterClose!.IsClosed.Should().BeFalse("poll2 should remain open");
    }

    // Helper class for response deserialization
    private class CreatePollResponse
    {
        public int PollId { get; set; }
        public string Code { get; set; } = "";
        public string Question { get; set; } = "";
    }
}
