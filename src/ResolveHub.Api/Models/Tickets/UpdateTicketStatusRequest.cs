using System.ComponentModel.DataAnnotations;
using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public class UpdateTicketStatusRequest
{
    [EnumDataType(typeof(TicketStatus))]
    public TicketStatus Status { get; set; }

    [StringLength(4000)]
    public string? ResolutionNotes { get; set; }
}