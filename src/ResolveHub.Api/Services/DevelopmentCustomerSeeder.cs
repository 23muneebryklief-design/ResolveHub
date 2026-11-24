using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Domain.Entities;
using ResolveHub.Domain.Enums;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Api.Services;

public static class DevelopmentCustomerSeeder
{
    private static readonly Guid DevelopmentOrganisationId =
        Guid.Parse("b2000000-0000-0000-0000-000000000001");

    private static readonly Guid DevelopmentCustomerId =
        Guid.Parse("c3000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var email = configuration["BootstrapCustomer:Email"];
        var password = configuration["BootstrapCustomer:Password"];

        if (string.IsNullOrWhiteSpace(email)
            && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Both BootstrapCustomer:Email and BootstrapCustomer:Password " +
                "must be configured to seed the development customer.");
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var organisation = await dbContext.Organisations
            .SingleOrDefaultAsync(
                item => item.Id == DevelopmentOrganisationId,
                cancellationToken);

        if (organisation is null)
        {
            dbContext.Organisations.Add(new Organisation
            {
                Id = DevelopmentOrganisationId,
                Name = "ResolveHub Development Organisation",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = DevelopmentCustomerId,
                UserName = email,
                Email = email,
                FirstName = "Development",
                LastName = "Customer",
                Role = UserRole.Customer,
                IsActive = true,
                OrganisationId = DevelopmentOrganisationId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    createResult.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Could not create the development customer: {errors}");
            }

            return;
        }

        user.FirstName = "Development";
        user.LastName = "Customer";
        user.Role = UserRole.Customer;
        user.IsActive = true;
        user.OrganisationId = DevelopmentOrganisationId;

        var updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            var errors = string.Join(
                "; ",
                updateResult.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Could not update the development customer: {errors}");
        }
    }
}
