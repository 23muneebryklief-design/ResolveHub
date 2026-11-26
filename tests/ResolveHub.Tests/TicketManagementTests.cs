
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

public sealed class TicketManagementTests
    : IClassFixture<ResolveHubApiFactory>
{
    private static readonly Guid OrganisationEId =
        Guid.Parse("b2000000-0000-0000-0000-000000000105");

    private static readonly Guid OrganisationFId =
        Guid.Parse("b2000000-0000-0000-0000-000000000106");

    private static readonly Guid AgentEId =
        Guid.Parse("c3000000-0000-0000-0000-000000000107");

    private static readonly Guid AssigneeEId =
        Guid.Parse("c3000000-0000-0000-0000-000000000108");

    private static readonly Guid AgentFId =
        Guid.Parse("c3000000-0000-0000-0000-000000000109");

    private static readonly Guid CustomerEId =
        Guid.Parse("c3000000-0000-0000-0000-000000000110");

    private static readonly Guid CustomerFId =
        Guid.Parse("c3000000-0000-0000-0000-000000000111");

    private static readonly Guid AdministratorId =
        Guid.Parse("c3000000-0000-0000-0000-000000000112");

    private static readonly Guid AssignmentTicketId =
        Guid.Parse("d4000000-0000-0000-0000-000000000107");

    private static readonly Guid InvalidAssigneeTicketId =
        Guid.Parse("d4000000-0000-0000-0000-000000000108");

    private static readonly Guid CrossOrganisationTicketId =
        Guid.Parse("d4000000-0000-0000-0000-000000000109");

    private static readonly Guid StatusFlowTicketId =
        Guid.Parse("d4000000-0000-0000-0000-000000000110");

    private static readonly Guid InvalidTransitionTicketId =
        Guid.Parse("d4000000-0000-0000-0000-000000000111");

    private static readonly Guid AdministratorTicketId =
        Guid.Parse("d4000000-0000-0000-0000-000000000112");

    private static readonly Guid CategoryId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private readonly ResolveHubApiFactory _factory;

    public TicketManagementTests(ResolveHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Support_agent_can_assign_ticket_in_its_organisation()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("SupportAgent", AgentEId);

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{AssignmentTicketId}/assign",
            new AssignTicketRequest
            {
                AssigneeId = AssigneeEId
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticket = await response.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(ticket);
        Assert.Equal(AssigneeEId, ticket.AssigneeId);
        Assert.NotNull(ticket.Assignee);
        Assert.Equal(AssigneeEId, ticket.Assignee.Id);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var history = await dbContext.TicketHistory
            .AsNoTracking()
            .SingleAsync(item => item.TicketId == AssignmentTicketId);

        Assert.Equal(AgentEId, history.ActorId);
        Assert.Equal(
            TicketHistoryEventType.AssignmentChanged,
            history.EventType);
        Assert.Null(history.PreviousValue);
        Assert.Equal(AssigneeEId.ToString(), history.NewValue);
        Assert.NotEqual(default, history.CreatedAtUtc);
    }

    [Fact]
    public async Task Assignment_rejects_assignee_from_another_organisation()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("SupportAgent", AgentEId);

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{InvalidAssigneeTicketId}/assign",
            new AssignTicketRequest
            {
                AssigneeId = AgentFId
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == InvalidAssigneeTicketId);

        Assert.Null(ticket.AssigneeId);

        Assert.False(await dbContext.TicketHistory.AnyAsync(
            item => item.TicketId == InvalidAssigneeTicketId));
    }

    [Fact]
    public async Task Support_agent_cannot_assign_ticket_in_another_organisation()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("SupportAgent", AgentEId);

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{CrossOrganisationTicketId}/assign",
            new AssignTicketRequest
            {
                AssigneeId = AgentFId
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        Assert.False(await dbContext.TicketHistory.AnyAsync(
            item => item.TicketId == CrossOrganisationTicketId));
    }

    [Fact]
    public async Task Customer_cannot_assign_tickets_or_change_status()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("Customer", CustomerEId);

        using var assignmentResponse = await client.PostAsJsonAsync(
            $"/api/Tickets/{AssignmentTicketId}/assign",
            new AssignTicketRequest
            {
                AssigneeId = AssigneeEId
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            assignmentResponse.StatusCode);

        using var statusResponse = await client.PatchAsJsonAsync(
            $"/api/Tickets/{StatusFlowTicketId}/status",
            new UpdateTicketStatusRequest
            {
                Status = TicketStatus.InProgress
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            statusResponse.StatusCode);
    }

    [Fact]
    public async Task Status_changes_require_resolution_notes_and_create_history()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("SupportAgent", AgentEId);

        // Open -> InProgress
        using var progressResponse = await client.PatchAsJsonAsync(
            $"/api/Tickets/{StatusFlowTicketId}/status",
            new UpdateTicketStatusRequest
            {
                Status = TicketStatus.InProgress
            });

        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);

        var progressTicket = await progressResponse.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(progressTicket);
        Assert.Equal(TicketStatus.InProgress, progressTicket.Status);

        // Resolution without notes must fail.
        using var missingNotesResponse = await client.PatchAsJsonAsync(
            $"/api/Tickets/{StatusFlowTicketId}/status",
            new UpdateTicketStatusRequest
            {
                Status = TicketStatus.Resolved
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            missingNotesResponse.StatusCode);

        // InProgress -> Resolved
        using var resolveResponse = await client.PatchAsJsonAsync(
            $"/api/Tickets/{StatusFlowTicketId}/status",
            new UpdateTicketStatusRequest
            {
                Status = TicketStatus.Resolved,
                ResolutionNotes = "The reported issue has been investigated and fixed."
            });

        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        var resolvedTicket = await resolveResponse.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(resolvedTicket);
        Assert.Equal(TicketStatus.Resolved, resolvedTicket.Status);

        // Resolved -> Closed
        using var closeResponse = await client.PatchAsJsonAsync(
            $"/api/Tickets/{StatusFlowTicketId}/status",
            new UpdateTicketStatusRequest
            {
                Status = TicketStatus.Closed
            });

        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var closedTicket = await closeResponse.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(closedTicket);
        Assert.Equal(TicketStatus.Closed, closedTicket.Status);

        // Closed -> Open
        using var reopenResponse = await client.PatchAsJsonAsync(
            $"/api/Tickets/{StatusFlowTicketId}/status",
            new UpdateTicketStatusRequest
            {
                Status = TicketStatus.Open
            });

        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);

        var reopenedTicket = await reopenResponse.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(reopenedTicket);
        Assert.Equal(TicketStatus.Open, reopenedTicket.Status);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == StatusFlowTicketId);

        Assert.Null(ticket.ResolutionNotes);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);

        var history = await dbContext.TicketHistory
            .AsNoTracking()
            .Where(item => item.TicketId == StatusFlowTicketId)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(4, history.Count);

        Assert.Equal(
            new[]
            {
                TicketHistoryEventType.StatusChanged,
                TicketHistoryEventType.Resolved,
                TicketHistoryEventType.Closed,
                TicketHistoryEventType.Reopened
            },
            history.Select(item => item.EventType).ToArray());

        Assert.All(history, entry =>
        {
            Assert.Equal(AgentEId, entry.ActorId);
            Assert.NotEqual(default, entry.CreatedAtUtc);
        });

        Assert.Equal("Open", history[0].PreviousValue);
        Assert.Equal("InProgress", history[0].NewValue);

        Assert.Equal("InProgress", history[1].PreviousValue);
        Assert.Equal("Resolved", history[1].NewValue);

        Assert.Equal("Resolved", history[2].PreviousValue);
        Assert.Equal("Closed", history[2].NewValue);

        Assert.Equal("Closed", history[3].PreviousValue);
        Assert.Equal("Open", history[3].NewValue);
    }

    [Fact]
    public async Task Invalid_status_transition_is_rejected()
    {
        await EnsureSeededAsync();

        using var client = CreateClient("SupportAgent", AgentEId);

        using var response = await client.PatchAsJsonAsync(
            $"/api/Tickets/{InvalidTransitionTicketId}/status",
            new UpdateTicketStatusRequest
            {
                Status = TicketStatus.Closed
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == InvalidTransitionTicketId);

        Assert.Equal(TicketStatus.Open, ticket.Status);

        Assert.False(await dbContext.TicketHistory.AnyAsync(
            item => item.TicketId == InvalidTransitionTicketId));
    }

    [Fact]
    public async Task Administrator_can_assign_ticket_across_organisations()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "Administrator",
            AdministratorId);

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{AdministratorTicketId}/assign",
            new AssignTicketRequest
            {
                AssigneeId = AgentFId
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticket = await response.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(ticket);
        Assert.Equal(OrganisationFId, ticket.OrganisationId);
        Assert.Equal(AgentFId, ticket.AssigneeId);

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var history = await dbContext.TicketHistory
            .AsNoTracking()
            .SingleAsync(item => item.TicketId == AdministratorTicketId);

        Assert.Equal(AdministratorId, history.ActorId);
        Assert.Equal(
            TicketHistoryEventType.AssignmentChanged,
            history.EventType);
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
                    role == "SupportAgent"
                        ? OrganisationEId
                        : role == "Customer"
                            ? OrganisationEId
                            : null));

        return client;
    }

    private async Task EnsureSeededAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();

        if (!await dbContext.Organisations.AnyAsync(
                item => item.Id == OrganisationEId))
        {
            dbContext.Organisations.Add(new Organisation
            {
                Id = OrganisationEId,
                Name = "Management Test Organisation E",
                IsActive = true
            });
        }

        if (!await dbContext.Organisations.AnyAsync(
                item => item.Id == OrganisationFId))
        {
            dbContext.Organisations.Add(new Organisation
            {
                Id = OrganisationFId,
                Name = "Management Test Organisation F",
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();

        AddUserIfMissing(
            dbContext,
            AgentEId,
            "management-agent-e@resolvehub.test",
            "Agent",
            "E",
            UserRole.SupportAgent,
            OrganisationEId);

        AddUserIfMissing(
            dbContext,
            AssigneeEId,
            "management-assignee-e@resolvehub.test",
            "Assignee",
            "E",
            UserRole.SupportAgent,
            OrganisationEId);

        AddUserIfMissing(
            dbContext,
            AgentFId,
            "management-agent-f@resolvehub.test",
            "Agent",
            "F",
            UserRole.SupportAgent,
            OrganisationFId);

        AddUserIfMissing(
            dbContext,
            CustomerEId,
            "management-customer-e@resolvehub.test",
            "Customer",
            "E",
            UserRole.Customer,
            OrganisationEId);

        AddUserIfMissing(
            dbContext,
            CustomerFId,
            "management-customer-f@resolvehub.test",
            "Customer",
            "F",
            UserRole.Customer,
            OrganisationFId);

        AddUserIfMissing(
            dbContext,
            AdministratorId,
            "management-admin@resolvehub.test",
            "Management",
            "Administrator",
            UserRole.Administrator,
            null);

        await dbContext.SaveChangesAsync();

        AddTicketIfMissing(
            dbContext,
            AssignmentTicketId,
            "TEST-MGMT-ASSIGN",
            OrganisationEId,
            CustomerEId,
            TicketStatus.Open);

        AddTicketIfMissing(
            dbContext,
            InvalidAssigneeTicketId,
            "TEST-MGMT-INVALID-ASSIGN",
            OrganisationEId,
            CustomerEId,
            TicketStatus.Open);

        AddTicketIfMissing(
            dbContext,
            CrossOrganisationTicketId,
            "TEST-MGMT-CROSS-ORG",
            OrganisationFId,
            CustomerFId,
            TicketStatus.Open);

        AddTicketIfMissing(
            dbContext,
            StatusFlowTicketId,
            "TEST-MGMT-STATUS-FLOW",
            OrganisationEId,
            CustomerEId,
            TicketStatus.Open);

        AddTicketIfMissing(
            dbContext,
            InvalidTransitionTicketId,
            "TEST-MGMT-INVALID-TRANSITION",
            OrganisationEId,
            CustomerEId,
            TicketStatus.Open);

        AddTicketIfMissing(
            dbContext,
            AdministratorTicketId,
            "TEST-MGMT-ADMIN",
            OrganisationFId,
            CustomerFId,
            TicketStatus.Open);

        await dbContext.SaveChangesAsync();
    }

    private static void AddUserIfMissing(
        ApplicationDbContext dbContext,
        Guid id,
        string email,
        string firstName,
        string lastName,
        UserRole role,
        Guid? organisationId)
    {
        if (dbContext.Users.Local.Any(user => user.Id == id))
        {
            return;
        }

        if (dbContext.Users.Any(user => user.Id == id))
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
            OrganisationId = organisationId,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static void AddTicketIfMissing(
        ApplicationDbContext dbContext,
        Guid id,
        string reference,
        Guid organisationId,
        Guid requesterId,
        TicketStatus status)
    {
        if (dbContext.Tickets.Local.Any(ticket => ticket.Id == id))
        {
            return;
        }

        if (dbContext.Tickets.Any(ticket => ticket.Id == id))
        {
            return;
        }

        dbContext.Tickets.Add(new Ticket
        {
            Id = id,
            Reference = reference,
            Subject = reference,
            Description = "Ticket management integration test.",
            CategoryId = CategoryId,
            Priority = TicketPriority.Normal,
            Status = status,
            OrganisationId = organisationId,
            RequesterId = requesterId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
