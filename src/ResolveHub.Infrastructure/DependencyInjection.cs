using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Domain.Interfaces;
using ResolveHub.Infrastructure.Data;
using ResolveHub.Infrastructure.Services;

namespace ResolveHub.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddScoped<ITicketReferenceGenerator, TicketReferenceGenerator>();
            services.AddScoped<TicketCreationService>();

            services.AddHealthChecks()
                .AddDbContextCheck<ApplicationDbContext>("database");

            return services;
        }
    }
}
