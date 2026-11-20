using ResolveHub.Domain.Enums;

namespace ResolveHub.Domain.Entities;

public class TicketHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }

    public Guid ActorId { get; set; }

    public TicketHistoryEventType EventType { get; set; }

    public string? PreviousValue { get; set; }

    public string? NewValue { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;

    public ApplicationUser Actor { get; set; } = null!;
}
