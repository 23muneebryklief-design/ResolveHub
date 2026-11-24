using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.Models.Tickets;

public sealed class AddTicketCommentRequest
{
    [Required]
    [MaxLength(4000)]
    public string Content { get; init; } = string.Empty;
}
