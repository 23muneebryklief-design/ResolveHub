
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ResolveHub.Api.Services;
using ResolveHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ResolveHub API",
        Version = "v1",
        Description = "Customer Support and Service Request Management API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter the JWT access token returned by POST /api/auth/login.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document =>
    {
        var requirement = new OpenApiSecurityRequirement();
        requirement.Add(
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>());
        return requirement;
    });
});

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer was not configured.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience was not configured.");

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT signing key was not configured.");

if (!int.TryParse(builder.Configuration["Jwt:ExpiresMinutes"], out var jwtExpiresMinutes)
    || jwtExpiresMinutes <= 0)
{
    throw new InvalidOperationException(
        "JWT expiry must be a positive number of minutes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Tickets.Create", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Customer", "SupportAgent", "Administrator");
    });

    options.AddPolicy("Tickets.Read", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Customer", "SupportAgent", "Administrator");
    });

    options.AddPolicy("Tickets.Comment", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Customer", "SupportAgent", "Administrator");
    });

    options.AddPolicy("Tickets.Assign", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("SupportAgent", "Administrator");
    });

    options.AddPolicy("Tickets.UpdateStatus", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("SupportAgent", "Administrator");
    });
});

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string was not configured.");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DevelopmentCustomerSeeder.SeedAsync(
        app.Services,
        app.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "ResolveHub API V1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}
