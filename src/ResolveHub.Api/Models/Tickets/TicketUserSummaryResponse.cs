namespace ResolveHub.Api.Models.Tickets;

public class TicketUserSummaryResponse
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;
}