using Microsoft.OpenApi;
using ResolveHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register controller support
builder.Services.AddControllers();

// Register API endpoint discovery
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ResolveHub API",
        Version = "v1",
        Description = "Customer Support and Service Request Management API"
    });
});



// Retrieve database connection string
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string was not configured.");

// Register Infrastructure services
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

// Enable Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "ResolveHub API V1"
        );
    });
}

// Configure HTTP request pipeline
app.UseHttpsRedirection();

app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health check
app.MapHealthChecks("/health");

app.Run();