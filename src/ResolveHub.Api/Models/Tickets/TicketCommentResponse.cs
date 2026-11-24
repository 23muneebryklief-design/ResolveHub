using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public sealed class TicketCommentResponse
{
    public Guid Id { get; init; }

    public Guid TicketId { get; init; }

    public Guid AuthorId { get; init; }

    public string Content { get; init; } = string.Empty;

    public CommentVisibility Visibility { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
