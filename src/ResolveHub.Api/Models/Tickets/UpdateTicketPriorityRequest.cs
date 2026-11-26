using System.ComponentModel.DataAnnotations;
using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public class UpdateTicketPriorityRequest
{
    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; set; }
}
