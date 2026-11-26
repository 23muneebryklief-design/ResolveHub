
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Models.Tickets;
using ResolveHub.Domain.Entities;
using ResolveHub.Domain.Enums;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Tests;

public sealed class TenantIsolationTests
    : IClassFixture<ResolveHubApiFactory>
{
    private static readonly Guid OrganisationAId =
        Guid.Parse("b2000000-0000-0000-0000-000000000101");

    private static readonly Guid OrganisationBId =
        Guid.Parse("b2000000-0000-0000-0000-000000000102");

    private static readonly Guid CustomerAId =
        Guid.Parse("c3000000-0000-0000-0000-000000000101");

    private static readonly Guid CustomerBId =
        Guid.Parse("c3000000-0000-0000-0000-000000000102");

    private static readonly Guid TicketAId =
        Guid.Parse("d4000000-0000-0000-0000-000000000101");

    private static readonly Guid TicketBId =
        Guid.Parse("d4000000-0000-0000-0000-000000000102");

    private static readonly Guid CategoryId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private readonly ResolveHubApiFactory _factory;

    public TenantIsolationTests(ResolveHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customer_ticket_list_only_contains_its_organisation()
    {
        await EnsureSeededAsync();
        using var client = CreateCustomerClient();

        using var response = await client.GetAsync("/api/Tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tickets = await response.Content
            .ReadFromJsonAsync<List<TicketResponse>>();

        Assert.NotNull(tickets);
        Assert.Contains(tickets, ticket => ticket.Id == TicketAId);
        Assert.DoesNotContain(tickets, ticket => ticket.Id == TicketBId);
    }

    [Fact]
    public async Task Customer_cannot_get_another_organisations_ticket()
    {
        await EnsureSeededAsync();
        using var client = CreateCustomerClient();

        using var response = await client.GetAsync(
            $"/api/Tickets/{TicketBId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_get_another_organisations_comments()
    {
        await EnsureSeededAsync();
        using var client = CreateCustomerClient();

        using var response = await client.GetAsync(
            $"/api/Tickets/{TicketBId}/comments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_add_a_comment_to_another_organisations_ticket()
    {
        await EnsureSeededAsync();
        using var client = CreateCustomerClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/Tickets/{TicketBId}/comments",
            new AddTicketCommentRequest
            {
                Content = "This must not be added."
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_comments_only_include_customer_visible_comments()
    {
        await EnsureSeededAsync();
        using var client = CreateCustomerClient();

        using var response = await client.GetAsync(
            $"/api/Tickets/{TicketAId}/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var comments = await response.Content
            .ReadFromJsonAsync<List<TicketCommentResponse>>();

        Assert.NotNull(comments);

        var comment = Assert.Single(comments);
        Assert.Equal("Customer visible comment", comment.Content);
    }

    [Fact]
    public async Task Customer_ticket_creation_uses_identity_organisation_and_requester()
    {
        await EnsureSeededAsync();
        using var client = CreateCustomerClient();

        using var response = await client.PostAsJsonAsync(
            "/api/Tickets",
            new CreateTicketRequest
            {
                Subject = "Tenant scoped ticket",
                Description = "Created with server-side tenant identity.",
                CategoryId = CategoryId,
                Priority = TicketPriority.Normal
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var ticket = await response.Content
            .ReadFromJsonAsync<TicketResponse>();

        Assert.NotNull(ticket);
        Assert.Equal(OrganisationAId, ticket.OrganisationId);
        Assert.Equal(CustomerAId, ticket.RequesterId);
    }

    [Fact]
    public async Task Customer_access_requires_an_organisation_claim()
    {
        await EnsureSeededAsync();

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                ResolveHubApiFactory.CreateToken(
                    "Customer",
                    CustomerAId));

        using var response = await client.GetAsync("/api/Tickets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateCustomerClient()
    {
        var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                ResolveHubApiFactory.CreateToken(
                    "Customer",
                    CustomerAId,
                    OrganisationAId));

        return client;
    }

    private async Task EnsureSeededAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();

        if (await dbContext.Tickets.AnyAsync(
                ticket => ticket.Id == TicketAId))
        {
            return;
        }

        var organisationA = new Organisation
        {
            Id = OrganisationAId,
            Name = "Tenant A"
        };

        var organisationB = new Organisation
        {
            Id = OrganisationBId,
            Name = "Tenant B"
        };

        var customerA = CreateCustomer(
            CustomerAId,
            "customer-a@resolvehub.test",
            organisationA);

        var customerB = CreateCustomer(
            CustomerBId,
            "customer-b@resolvehub.test",
            organisationB);

        dbContext.Organisations.AddRange(
            organisationA,
            organisationB);

        dbContext.Users.AddRange(
            customerA,
            customerB);

        dbContext.Tickets.AddRange(
            CreateTicket(
                TicketAId,
                "TEST-TENANT-A",
                OrganisationAId,
                CustomerAId),
            CreateTicket(
                TicketBId,
                "TEST-TENANT-B",
                OrganisationBId,
                CustomerBId));

        dbContext.TicketComments.AddRange(
            new TicketComment
            {
                Id = Guid.Parse("e5000000-0000-0000-0000-000000000101"),
                TicketId = TicketAId,
                AuthorId = CustomerAId,
                Content = "Customer visible comment",
                Visibility = CommentVisibility.CustomerVisible
            },
            new TicketComment
            {
                Id = Guid.Parse("e5000000-0000-0000-0000-000000000102"),
                TicketId = TicketAId,
                AuthorId = CustomerAId,
                Content = "Internal comment",
                Visibility = CommentVisibility.Internal
            },
            new TicketComment
            {
                Id = Guid.Parse("e5000000-0000-0000-0000-000000000103"),
                TicketId = TicketBId,
                AuthorId = CustomerBId,
                Content = "Other organisation comment",
                Visibility = CommentVisibility.CustomerVisible
            });

        await dbContext.SaveChangesAsync();
    }

    private static ApplicationUser CreateCustomer(
        Guid id,
        string email,
        Organisation organisation)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Test",
            LastName = "Customer",
            Role = UserRole.Customer,
            OrganisationId = organisation.Id,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static Ticket CreateTicket(
        Guid id,
        string reference,
        Guid organisationId,
        Guid requesterId)
    {
        return new Ticket
        {
            Id = id,
            Reference = reference,
            Subject = reference,
            Description = "Tenant isolation test ticket.",
            CategoryId = CategoryId,
            Priority = TicketPriority.Normal,
            Status = TicketStatus.Open,
            OrganisationId = organisationId,
            RequesterId = requesterId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
