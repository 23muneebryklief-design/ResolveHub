
using System.ComponentModel.DataAnnotations;
using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public class CreateTicketRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
}
