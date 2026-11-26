using System.ComponentModel.DataAnnotations;
using ResolveHub.Domain.Enums;

namespace ResolveHub.Api.Models.Tickets;

public sealed class AddTicketCommentRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;

    [EnumDataType(typeof(CommentVisibility))]
    public CommentVisibility Visibility { get; init; }
        = CommentVisibility.CustomerVisible;
}