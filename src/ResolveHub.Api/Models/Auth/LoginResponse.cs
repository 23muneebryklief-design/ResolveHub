namespace ResolveHub.Api.Models.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string TokenType { get; init; } = "Bearer";

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
