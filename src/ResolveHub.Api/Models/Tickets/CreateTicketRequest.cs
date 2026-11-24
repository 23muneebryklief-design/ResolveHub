
using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public class CreateTicketRequest
{
    public string Subject { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public Guid OrganisationId { get; set; }

    public Guid RequesterId { get; set; }
}
