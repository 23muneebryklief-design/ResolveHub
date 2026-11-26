
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Models.Tickets;
using ResolveHub.Domain.Entities;
using ResolveHub.Domain.Enums;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Tests;

public sealed class TicketCommentTests
    : IClassFixture<ResolveHubApiFactory>
{
    private static readonly Guid OrganisationId =
        Guid.Parse("b2000000-0000-0000-0000-000000000201");

    private static readonly Guid CustomerId =
        Guid.Parse("c3000000-0000-0000-0000-000000000201");

    private static readonly Guid AgentId =
        Guid.Parse("c3000000-0000-0000-0000-000000000202");

    private static readonly Guid TicketId =
        Guid.Parse("d4000000-0000-0000-0000-000000000201");

    private static readonly Guid CategoryId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private readonly ResolveHubApiFactory _factory;

    public TicketCommentTests(ResolveHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customer_can_add_customer_visible_comment_and_history()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("Customer", CustomerId);

        var timestampBeforeRequest = DateTimeOffset.UtcNow;

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{TicketId}/comments",
            new AddTicketCommentRequest
            {
                Content = "Customer communication test comment.",
                Visibility = CommentVisibility.CustomerVisible
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var comment = await response.Content
            .ReadFromJsonAsync<TicketCommentResponse>();

        Assert.NotNull(comment);
        Assert.Equal(TicketId, comment.TicketId);
        Assert.Equal(CustomerId, comment.AuthorId);
        Assert.Equal(
            "Customer communication test comment.",
            comment.Content);
        Assert.Equal(
            CommentVisibility.CustomerVisible,
            comment.Visibility);
        Assert.True(comment.CreatedAtUtc >= timestampBeforeRequest);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var historyEntry = await dbContext.TicketHistory
            .AsNoTracking()
            .SingleAsync(entry => entry.TicketId == TicketId
                && entry.ActorId == CustomerId
                && entry.EventType == TicketHistoryEventType.CommentAdded
                && entry.NewValue == CommentVisibility.CustomerVisible.ToString()
                && entry.CreatedAtUtc == comment.CreatedAtUtc);

        Assert.Null(historyEntry.PreviousValue);
        Assert.Equal(comment.CreatedAtUtc, historyEntry.CreatedAtUtc);
    }

    [Fact]
    public async Task Customer_cannot_create_internal_note()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("Customer", CustomerId);

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{TicketId}/comments",
            new AddTicketCommentRequest
            {
                Content = "This should not be saved as an internal note.",
                Visibility = CommentVisibility.Internal
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        Assert.False(await dbContext.TicketComments
            .AnyAsync(comment =>
                comment.TicketId == TicketId
                && comment.Content ==
                    "This should not be saved as an internal note."));

        Assert.False(await dbContext.TicketHistory
            .AnyAsync(entry =>
                entry.TicketId == TicketId
                && entry.ActorId == CustomerId
                && entry.EventType ==
                    TicketHistoryEventType.CommentAdded
                && entry.NewValue == CommentVisibility.Internal.ToString()));
    }

    [Fact]
    public async Task Support_agent_can_add_internal_note_and_customers_cannot_see_it()
    {
        await EnsureSeededAsync();

        const string internalNoteContent =
            "BE014 internal note privacy test.";

        using var agentClient = CreateClient("SupportAgent", AgentId);

        using var createResponse = await agentClient.PostAsJsonAsync(
            $"/api/Tickets/{TicketId}/comments",
            new AddTicketCommentRequest
            {
                Content = internalNoteContent,
                Visibility = CommentVisibility.Internal
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdComment = await createResponse.Content
            .ReadFromJsonAsync<TicketCommentResponse>();

        Assert.NotNull(createdComment);
        Assert.Equal(AgentId, createdComment.AuthorId);
        Assert.Equal(CommentVisibility.Internal, createdComment.Visibility);
        Assert.Equal(internalNoteContent, createdComment.Content);

        // Verify internal users can see the note.
        using var agentGetResponse = await agentClient.GetAsync(
            $"/api/Tickets/{TicketId}/comments");

        Assert.Equal(HttpStatusCode.OK, agentGetResponse.StatusCode);

        var agentComments = await agentGetResponse.Content
            .ReadFromJsonAsync<List<TicketCommentResponse>>();

        Assert.NotNull(agentComments);

        Assert.Contains(
            agentComments,
            comment => comment.Id == createdComment.Id
                && comment.Content == internalNoteContent
                && comment.Visibility == CommentVisibility.Internal);

        // Verify customers cannot see the internal note.
        using var customerClient = CreateClient("Customer", CustomerId);

        using var customerGetResponse = await customerClient.GetAsync(
            $"/api/Tickets/{TicketId}/comments");

        Assert.Equal(HttpStatusCode.OK, customerGetResponse.StatusCode);

        var customerComments = await customerGetResponse.Content
            .ReadFromJsonAsync<List<TicketCommentResponse>>();

        Assert.NotNull(customerComments);

        Assert.DoesNotContain(
            customerComments,
            comment => comment.Id == createdComment.Id);

        Assert.DoesNotContain(
            customerComments,
            comment => comment.Content == internalNoteContent);

        // Verify audit entry does not contain the note's content.
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var historyEntry = await dbContext.TicketHistory
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.TicketId == TicketId
                && entry.ActorId == AgentId
                && entry.EventType ==
                    TicketHistoryEventType.CommentAdded
                && entry.CreatedAtUtc == createdComment.CreatedAtUtc);

        Assert.Equal(
            CommentVisibility.Internal.ToString(),
            historyEntry.NewValue);

        Assert.Null(historyEntry.PreviousValue);

        Assert.DoesNotContain(
            internalNoteContent,
            historyEntry.NewValue ?? string.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task Empty_or_whitespace_comment_is_rejected(
        string content)
    {
        await EnsureSeededAsync();

        using var client = CreateClient("Customer", CustomerId);

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{TicketId}/comments",
            new AddTicketCommentRequest
            {
                Content = content,
                Visibility = CommentVisibility.CustomerVisible
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient(string role, Guid userId)
    {
        var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                ResolveHubApiFactory.CreateToken(
                    role,
                    userId,
                    OrganisationId));

        return client;
    }

    private async Task EnsureSeededAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();

        if (!await dbContext.Organisations
            .AnyAsync(organisation => organisation.Id == OrganisationId))
        {
            dbContext.Organisations.Add(new Organisation
            {
                Id = OrganisationId,
                Name = "BE-014 Comment Test Organisation",
                IsActive = true
            });

            await dbContext.SaveChangesAsync();
        }

        AddUserIfMissing(
            dbContext,
            CustomerId,
            "be014-customer@resolvehub.test",
            "BE014",
            "Customer",
            UserRole.Customer);

        AddUserIfMissing(
            dbContext,
            AgentId,
            "be014-agent@resolvehub.test",
            "BE014",
            "Agent",
            UserRole.SupportAgent);

        await dbContext.SaveChangesAsync();

        if (!await dbContext.Tickets
            .AnyAsync(ticket => ticket.Id == TicketId))
        {
            dbContext.Tickets.Add(new Ticket
            {
                Id = TicketId,
                Reference = "TEST-BE014-COMMENTS",
                Subject = "BE-014 Comment Test Ticket",
                Description = "Ticket for comment visibility tests.",
                CategoryId = CategoryId,
                Priority = TicketPriority.Normal,
                Status = TicketStatus.Open,
                OrganisationId = OrganisationId,
                RequesterId = CustomerId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }
    }

    private static void AddUserIfMissing(
        ApplicationDbContext dbContext,
        Guid id,
        string email,
        string firstName,
        string lastName,
        UserRole role)
    {
        if (dbContext.Users.Local.Any(user => user.Id == id)
            || dbContext.Users.Any(user => user.Id == id))
        {
            return;
        }

        dbContext.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            OrganisationId = OrganisationId,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
