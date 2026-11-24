using Microsoft.AspNetCore.Identity;
using ResolveHub.Domain.Enums;

namespace ResolveHub.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.NewGuid();
    }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? OrganisationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Organisation? Organisation { get; set; }

    public ICollection<Ticket> RequestedTickets { get; set; }
        = new List<Ticket>();

    public ICollection<Ticket> AssignedTickets { get; set; }
        = new List<Ticket>();

    public ICollection<TicketComment> Comments { get; set; }
        = new List<TicketComment>();

    public ICollection<TicketHistory> HistoryEntries { get; set; }
        = new List<TicketHistory>();
}