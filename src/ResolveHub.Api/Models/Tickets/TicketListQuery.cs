using System.ComponentModel.DataAnnotations;
using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public class TicketListQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [EnumDataType(typeof(TicketStatus))]
    public TicketStatus? Status { get; set; }

    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority? Priority { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? AssigneeId { get; set; }
}