using ResolveHub.Domain.Enums;

namespace ResolveHub.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Reference { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    // Organisation relationship
    public Guid OrganisationId { get; set; }

    public Organisation Organisation { get; set; } = null!;

    // Requester relationship
    public Guid RequesterId { get; set; }

    public ApplicationUser Requester { get; set; } = null!;

    // Assignee relationship - optional until assigned
    public Guid? AssigneeId { get; set; }

    public ApplicationUser? Assignee { get; set; }

    // Service target timestamps
    public DateTimeOffset? FirstResponseDueAtUtc { get; set; }

    public DateTimeOffset? ResolutionDueAtUtc { get; set; }

    public DateTimeOffset? FirstRespondedAtUtc { get; set; }

    // Resolution details
    public string? ResolutionNotes { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    // Record timestamps
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<TicketComment> Comments { get; set; }
        = new List<TicketComment>();

    public ICollection<TicketHistory> HistoryEntries { get; set; }
        = new List<TicketHistory>();
}
