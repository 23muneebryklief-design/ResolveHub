using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public class TicketResponse
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }

    public Guid OrganisationId { get; set; }

    public Guid RequesterId { get; set; }

    public TicketUserSummaryResponse? Requester { get; set; }

    public Guid? AssigneeId { get; set; }

    public TicketUserSummaryResponse? Assignee { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}