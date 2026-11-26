
using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public class TicketHistoryResponse
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Guid ActorId { get; set; }

    public TicketUserSummaryResponse Actor { get; set; } = null!;

    public TicketHistoryEventType EventType { get; set; }

    public string? PreviousValue { get; set; }

    public string? NewValue { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
