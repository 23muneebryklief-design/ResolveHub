namespace ResolveHub.Api.Models.Tickets;

public class TicketListResponse
{
    public IReadOnlyList<TicketResponse> Items { get; set; }
        = Array.Empty<TicketResponse>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}