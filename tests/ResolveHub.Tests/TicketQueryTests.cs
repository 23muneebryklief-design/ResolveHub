
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

public sealed class TicketQueryTests
    : IClassFixture<ResolveHubApiFactory>
{
    private static readonly Guid OrganisationCId =
        Guid.Parse("b2000000-0000-0000-0000-000000000103");

    private static readonly Guid OrganisationDId =
        Guid.Parse("b2000000-0000-0000-0000-000000000104");

    private static readonly Guid CustomerCId =
        Guid.Parse("c3000000-0000-0000-0000-000000000103");

    private static readonly Guid CustomerDId =
        Guid.Parse("c3000000-0000-0000-0000-000000000104");

    private static readonly Guid AgentCId =
        Guid.Parse("c3000000-0000-0000-0000-000000000105");

    private static readonly Guid AgentDId =
        Guid.Parse("c3000000-0000-0000-0000-000000000106");

    private static readonly Guid TicketC1Id =
        Guid.Parse("d4000000-0000-0000-0000-000000000103");

    private static readonly Guid TicketC2Id =
        Guid.Parse("d4000000-0000-0000-0000-000000000104");

    private static readonly Guid TicketC3Id =
        Guid.Parse("d4000000-0000-0000-0000-000000000105");

    private static readonly Guid TicketD1Id =
        Guid.Parse("d4000000-0000-0000-0000-000000000106");

    private static readonly Guid CategoryId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private readonly ResolveHubApiFactory _factory;

    public TicketQueryTests(ResolveHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ticket_list_returns_paginated_results()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "Customer",
            CustomerCId,
            OrganisationCId);

        using var firstResponse = await client.GetAsync(
            "/api/Tickets?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstPage = await firstResponse.Content
            .ReadFromJsonAsync<TicketListResponse>();

        Assert.NotNull(firstPage);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(2, firstPage.Items.Count);

        using var secondResponse = await client.GetAsync(
            "/api/Tickets?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondPage = await secondResponse.Content
            .ReadFromJsonAsync<TicketListResponse>();

        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(2, secondPage.PageSize);
        Assert.Single(secondPage.Items);
        Assert.Equal(TicketC1Id, secondPage.Items[0].Id);
    }

    [Fact]
    public async Task Ticket_list_filters_by_status_priority_category_and_assignee()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "Customer",
            CustomerCId,
            OrganisationCId);

        var statusResults = await GetTicketPageAsync(
            client,
            "/api/Tickets?status=InProgress");

        Assert.Equal(2, statusResults.TotalCount);

        var priorityResults = await GetTicketPageAsync(
            client,
            "/api/Tickets?priority=High");

        Assert.Equal(1, priorityResults.TotalCount);
        Assert.Equal(TicketC2Id, Assert.Single(priorityResults.Items).Id);

        var categoryResults = await GetTicketPageAsync(
            client,
            $"/api/Tickets?categoryId={CategoryId}");

        Assert.Equal(3, categoryResults.TotalCount);

        var assigneeResults = await GetTicketPageAsync(
            client,
            $"/api/Tickets?assigneeId={AgentCId}");

        Assert.Equal(2, assigneeResults.TotalCount);

        Assert.All(
            assigneeResults.Items,
            ticket => Assert.Equal(AgentCId, ticket.AssigneeId));
    }

    [Fact]
    public async Task Support_agent_only_sees_tickets_in_its_organisation()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "SupportAgent",
            AgentCId,
            OrganisationCId);

        using var response = await client.GetAsync("/api/Tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticketPage = await response.Content
            .ReadFromJsonAsync<TicketListResponse>();

        Assert.NotNull(ticketPage);
        Assert.Equal(3, ticketPage.TotalCount);

        Assert.Contains(
            ticketPage.Items,
            ticket => ticket.Id == TicketC1Id);

        Assert.DoesNotContain(
            ticketPage.Items,
            ticket => ticket.Id == TicketD1Id);

        using var otherOrganisationResponse = await client.GetAsync(
            $"/api/Tickets/{TicketD1Id}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            otherOrganisationResponse.StatusCode);
    }

    [Fact]
    public async Task Support_agent_access_requires_an_organisation_claim()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "SupportAgent",
            AgentCId,
            null);

        using var response = await client.GetAsync("/api/Tickets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_can_view_tickets_across_organisations()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "Administrator",
            Guid.NewGuid(),
            null);

        using var listResponse = await client.GetAsync("/api/Tickets");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var ticketPage = await listResponse.Content
            .ReadFromJsonAsync<TicketListResponse>();

        Assert.NotNull(ticketPage);

        Assert.Contains(
            ticketPage.Items,
            ticket => ticket.Id == TicketC1Id);

        Assert.Contains(
            ticketPage.Items,
            ticket => ticket.Id == TicketD1Id);

        using var detailResponse = await client.GetAsync(
            $"/api/Tickets/{TicketD1Id}");

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Ticket_detail_includes_requester_and_assignee_information()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "Customer",
            CustomerCId,
            OrganisationCId);

        using var response = await client.GetAsync(
            $"/api/Tickets/{TicketC1Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticket = await response.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(ticket);

        Assert.Equal(TicketC1Id, ticket.Id);
        Assert.Equal(CategoryId, ticket.CategoryId);
        Assert.False(string.IsNullOrWhiteSpace(ticket.CategoryName));

        Assert.NotNull(ticket.Requester);
        Assert.Equal(CustomerCId, ticket.Requester.Id);
        Assert.Equal("Test", ticket.Requester.FirstName);

        Assert.Equal(AgentCId, ticket.AssigneeId);
        Assert.NotNull(ticket.Assignee);
        Assert.Equal(AgentCId, ticket.Assignee.Id);
        Assert.Equal("Support", ticket.Assignee.FirstName);

        Assert.NotEqual(default, ticket.UpdatedAtUtc);
    }

    [Fact]
    public async Task Ticket_list_rejects_invalid_page_numbers()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "Customer",
            CustomerCId,
            OrganisationCId);

        using var response = await client.GetAsync(
            "/api/Tickets?page=0&pageSize=20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ticket_list_rejects_page_sizes_above_the_limit()
    {
        await EnsureSeededAsync();

        using var client = CreateClient(
            "Customer",
            CustomerCId,
            OrganisationCId);

        using var response = await client.GetAsync(
            "/api/Tickets?page=1&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient(
        string role,
        Guid userId,
        Guid? organisationId)
    {
        var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                ResolveHubApiFactory.CreateToken(
                    role,
                    userId,
                    organisationId));

        return client;
    }

    private static async Task<TicketListResponse> GetTicketPageAsync(
        HttpClient client,
        string requestUri)
    {
        using var response = await client.GetAsync(requestUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<TicketListResponse>();

        Assert.NotNull(result);

        return result;
    }

    private async Task EnsureSeededAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();

        if (!await dbContext.Organisations.AnyAsync(
                organisation => organisation.Id == OrganisationCId))
        {
            dbContext.Organisations.Add(new Organisation
            {
                Id = OrganisationCId,
                Name = "Query Test Organisation C",
                IsActive = true
            });
        }

        if (!await dbContext.Organisations.AnyAsync(
                organisation => organisation.Id == OrganisationDId))
        {
            dbContext.Organisations.Add(new Organisation
            {
                Id = OrganisationDId,
                Name = "Query Test Organisation D",
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();

        if (!await dbContext.Users.AnyAsync(
                user => user.Id == CustomerCId))
        {
            dbContext.Users.Add(CreateUser(
                CustomerCId,
                "query-customer-c@resolvehub.test",
                "Test",
                "Customer C",
                UserRole.Customer,
                OrganisationCId));
        }

        if (!await dbContext.Users.AnyAsync(
                user => user.Id == CustomerDId))
        {
            dbContext.Users.Add(CreateUser(
                CustomerDId,
                "query-customer-d@resolvehub.test",
                "Test",
                "Customer D",
                UserRole.Customer,
                OrganisationDId));
        }

        if (!await dbContext.Users.AnyAsync(
                user => user.Id == AgentCId))
        {
            dbContext.Users.Add(CreateUser(
                AgentCId,
                "query-agent-c@resolvehub.test",
                "Support",
                "Agent C",
                UserRole.SupportAgent,
                OrganisationCId));
        }

        if (!await dbContext.Users.AnyAsync(
                user => user.Id == AgentDId))
        {
            dbContext.Users.Add(CreateUser(
                AgentDId,
                "query-agent-d@resolvehub.test",
                "Support",
                "Agent D",
                UserRole.SupportAgent,
                OrganisationDId));
        }

        await dbContext.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;

        if (!await dbContext.Tickets.AnyAsync(
                ticket => ticket.Id == TicketC1Id))
        {
            dbContext.Tickets.Add(CreateTicket(
                TicketC1Id,
                "TEST-QUERY-C1",
                OrganisationCId,
                CustomerCId,
                TicketStatus.Open,
                TicketPriority.Normal,
                AgentCId,
                now.AddMinutes(-3)));
        }

        if (!await dbContext.Tickets.AnyAsync(
                ticket => ticket.Id == TicketC2Id))
        {
            dbContext.Tickets.Add(CreateTicket(
                TicketC2Id,
                "TEST-QUERY-C2",
                OrganisationCId,
                CustomerCId,
                TicketStatus.InProgress,
                TicketPriority.High,
                AgentCId,
                now.AddMinutes(-2)));
        }

        if (!await dbContext.Tickets.AnyAsync(
                ticket => ticket.Id == TicketC3Id))
        {
            dbContext.Tickets.Add(CreateTicket(
                TicketC3Id,
                "TEST-QUERY-C3",
                OrganisationCId,
                CustomerCId,
                TicketStatus.InProgress,
                TicketPriority.Normal,
                null,
                now.AddMinutes(-1)));
        }

        if (!await dbContext.Tickets.AnyAsync(
                ticket => ticket.Id == TicketD1Id))
        {
            dbContext.Tickets.Add(CreateTicket(
                TicketD1Id,
                "TEST-QUERY-D1",
                OrganisationDId,
                CustomerDId,
                TicketStatus.Open,
                TicketPriority.Normal,
                AgentDId,
                now));
        }

        await dbContext.SaveChangesAsync();
    }

    private static ApplicationUser CreateUser(
        Guid id,
        string email,
        string firstName,
        string lastName,
        UserRole role,
        Guid organisationId)
    {
        return new ApplicationUser
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
        };
    }

    private static Ticket CreateTicket(
        Guid id,
        string reference,
        Guid organisationId,
        Guid requesterId,
        TicketStatus status,
        TicketPriority priority,
        Guid? assigneeId,
        DateTimeOffset createdAtUtc)
    {
        return new Ticket
        {
            Id = id,
            Reference = reference,
            Subject = reference,
            Description = "Ticket query test description.",
            CategoryId = CategoryId,
            Priority = priority,
            Status = status,
            OrganisationId = organisationId,
            RequesterId = requesterId,
            AssigneeId = assigneeId,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }
}
