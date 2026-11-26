
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ResolveHub.Tests;

public sealed class AuthorizationTests
    : IClassFixture<ResolveHubApiFactory>
{
    private readonly ResolveHubApiFactory _factory;

    public AuthorizationTests(ResolveHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ticket_creation_rejects_anonymous_requests()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/Tickets",
            InvalidJson());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ticket_creation_rejects_invalid_tokens()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid-token");

        using var response = await client.PostAsync(
            "/api/Tickets",
            InvalidJson());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ticket_creation_forbids_roles_outside_the_policy()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                ResolveHubApiFactory.CreateToken("OperationsManager"));

        using var response = await client.PostAsync(
            "/api/Tickets",
            InvalidJson());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("SupportAgent")]
    [InlineData("Administrator")]
    public async Task Ticket_creation_allows_roles_in_the_policy(
        string role)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                ResolveHubApiFactory.CreateToken(role));

        using var response = await client.PostAsync(
            "/api/Tickets",
            InvalidJson());

        // Authorization passed; malformed JSON is rejected by model binding.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static StringContent InvalidJson()
    {
        return new StringContent(
            "{",
            Encoding.UTF8,
            "application/json");
    }
}

public sealed class ResolveHubApiFactory
    : WebApplicationFactory<Program>
{
    private const string Issuer = "ResolveHub.Tests";
    private const string Audience = "ResolveHub.Tests.Client";
    private const string SigningKey =
        "test-only-signing-key-with-more-than-32-bytes-1234567890";
    private const string TestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ResolveHubAuthorizationTests;" +
        "Trusted_Connection=True;MultipleActiveResultSets=True";

    static ResolveHubApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            TestConnectionString);
        Environment.SetEnvironmentVariable("Jwt__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", Audience);
        Environment.SetEnvironmentVariable("Jwt__Key", SigningKey);
        Environment.SetEnvironmentVariable("Jwt__ExpiresMinutes", "5");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:Key"] = SigningKey,
                    ["Jwt:ExpiresMinutes"] = "5",
                    ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                    ["BootstrapCustomer:Email"] = string.Empty,
                    ["BootstrapCustomer:Password"] = string.Empty
                });
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(SigningKey)),
                            ValidateIssuer = true,
                            ValidIssuer = Issuer,
                            ValidateAudience = true,
                            ValidAudience = Audience,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds(30),
                            NameClaimType = ClaimTypes.NameIdentifier,
                            RoleClaimType = ClaimTypes.Role
                        };
                });
        });
    }

    public static string CreateToken(
        string role,
        Guid? userId = null,
        Guid? organisationId = null)
    {
        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                (userId ?? Guid.NewGuid()).ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        if (organisationId.HasValue)
        {
            claims.Add(new Claim(
                "organisation_id",
                organisationId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(SigningKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
