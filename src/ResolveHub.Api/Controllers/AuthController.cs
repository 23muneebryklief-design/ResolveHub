using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ResolveHub.Api.Models.Auth;
using ResolveHub.Domain.Entities;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null
            || !user.IsActive
            || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new
            {
                error = "Invalid email or password."
            });
        }

        var issuer = _configuration["Jwt:Issuer"]!;
        var audience = _configuration["Jwt:Audience"]!;
        var key = _configuration["Jwt:Key"]!;
        var expiresMinutes = int.Parse(
            _configuration["Jwt:ExpiresMinutes"]!);

        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(ClaimTypes.Email, user.Email ?? request.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.OrganisationId.HasValue)
        {
            claims.Add(new Claim(
                "organisation_id",
                user.OrganisationId.Value.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc
        });
    }
}