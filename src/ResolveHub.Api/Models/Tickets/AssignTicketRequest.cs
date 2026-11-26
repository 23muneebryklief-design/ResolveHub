using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.Models.Tickets;

public class AssignTicketRequest
{
    [Required]
    public Guid AssigneeId { get; set; }
}
