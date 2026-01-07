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
/// Integration tests for User Story 5: Closed poll voting block
/// Tests that votes cannot be submitted to closed polls
/// </summary>
public class ClosedPollTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ClosedPollTests(WebApplicationFactory<Program> factory)
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

                // Replace DbContext with in-memory database for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PollDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<PollDbContext>(options =>
                {
                    options.UseInMemoryDatabase("ClosedPollTests_" + Guid.NewGuid());
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task VoteSubmission_ShouldFailWhenPollIsClosed()
    {
        // Arrange - Create a closed poll
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var poll = new Poll
        {
            Code = "CLSD",
            Question = "Closed poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            ClosedAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.Polls.Add(poll);
        
        var option = new Option
        {
            PollId = poll.Id,
            Text = "Option A",
            DisplayOrder = 0
        };
        poll.Options.Add(option);
        
        await context.SaveChangesAsync();

        // Act - Try to submit a vote to the closed poll
        var voteRequest = new
        {
            selectedOptionId = option.Id
        };
        var response = await _client.PostAsJsonAsync($"/p/{poll.Code}/vote", voteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "voting on closed poll should be rejected");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("closed", "error message should indicate poll is closed");
    }

    [Fact]
    public async Task GetVotePage_ShouldShowClosedMessageWhenPollIsClosed()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var poll = new Poll
        {
            Code = "SHOW",
            Question = "Display test poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = true,
            ClosedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/p/{poll.Code}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("closed", "page should indicate poll is closed");
    }

    [Fact]
    public async Task ManualClosePoll_ShouldPreventSubsequentVotes()
    {
        // Arrange - Create an open poll
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PollDbContext>();
        
        var poll = new Poll
        {
            Code = "OPEN",
            Question = "Open poll",
            ChoiceMode = ChoiceMode.Single,
            IsClosed = false,
            CreatedAt = DateTime.UtcNow
        };
        var option = new Option { PollId = poll.Id, Text = "Choice", DisplayOrder = 0 };
        poll.Options.Add(option);
        context.Polls.Add(poll);
        await context.SaveChangesAsync();

        // Act - Close the poll via API
        var hostToken = "test-host-token"; // Assumes config has this token
        _client.DefaultRequestHeaders.Add("X-Host-Token", hostToken);
        var closeResponse = await _client.PostAsync($"/host/polls/{poll.Code}/close", null);
        
        // Then try to vote
        var voteResponse = await _client.PostAsJsonAsync($"/p/{poll.Code}/vote", new { selectedOptionId = option.Id });

        // Assert
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK, "close should succeed");
        voteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, "vote should be rejected after manual close");
    }
}
