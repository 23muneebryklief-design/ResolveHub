using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.Models.Auth;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}