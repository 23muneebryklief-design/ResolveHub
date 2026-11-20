using ResolveHub.Domain.Enums;

namespace ResolveHub.Domain.Entities;

public class TicketComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }

    public Guid AuthorId { get; set; }

    public string Content { get; set; } = string.Empty;

    public CommentVisibility Visibility { get; set; }
        = CommentVisibility.CustomerVisible;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Ticket Ticket { get; set; } = null!;

    public ApplicationUser Author { get; set; } = null!;
}
