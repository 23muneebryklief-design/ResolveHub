using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Models.Auth;
using ResolveHub.Domain.Entities;
using ResolveHub.Domain.Enums;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Tests;

public sealed class AuthenticationTests
    : IClassFixture<ResolveHubApiFactory>
{
    private readonly ResolveHubApiFactory _factory;

    public AuthenticationTests(ResolveHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_issues_a_bearer_token_for_a_valid_identity_password()
    {
        const string password = "StrongPassword123!";
        var email = $"{Guid.NewGuid():N}@resolvehub.test";
        var user = await CreateUserAsync(email, password);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = password
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.Equal("Bearer", login.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(login.ExpiresAtUtc > DateTimeOffset.UtcNow);

        var jwt = new JwtSecurityTokenHandler()
            .ReadJwtToken(login.AccessToken);
        Assert.Contains(
            jwt.Claims,
            claim => claim.Value == user.Id.ToString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var persistedUser = await userManager.FindByEmailAsync(email);

        Assert.NotNull(persistedUser);
        Assert.False(string.IsNullOrWhiteSpace(persistedUser.PasswordHash));
        Assert.NotEqual(password, persistedUser.PasswordHash);
    }

    [Fact]
    public async Task Login_rejects_an_incorrect_password()
    {
        var email = $"{Guid.NewGuid():N}@resolvehub.test";
        await CreateUserAsync(email, "StrongPassword123!");

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = "WrongPassword123!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", content);
    }

    [Fact]
    public async Task Login_rejects_an_unknown_email()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = $"{Guid.NewGuid():N}@resolvehub.test",
                Password = "StrongPassword123!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Authentication",
            LastName = "Test",
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Errors.Select(error => error.Description)));

        return user;
    }
}
