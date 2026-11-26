
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Domain.Entities;
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

            services
                .AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddScoped<ITicketReferenceGenerator, TicketReferenceGenerator>();
            services.AddScoped<TicketCreationService>();
            services.AddScoped<TicketManagementService>();

            services.AddHealthChecks()
                .AddDbContextCheck<ApplicationDbContext>("database");

            return services;
        }
    }
}
