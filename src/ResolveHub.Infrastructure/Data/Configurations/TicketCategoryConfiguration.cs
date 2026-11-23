using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResolveHub.Domain.Entities;

namespace ResolveHub.Infrastructure.Data.Configurations;

public class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.ToTable("TicketCategories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasData(
            new TicketCategory
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                Code = "ACCOUNT_ACCESS",
                Name = "Account & Access",
                IsActive = true
            },
            new TicketCategory
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                Code = "BILLING_PAYMENTS",
                Name = "Billing & Payments",
                IsActive = true
            },
            new TicketCategory
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                Code = "TECHNICAL_SUPPORT",
                Name = "Technical Support",
                IsActive = true
            },
            new TicketCategory
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"),
                Code = "BUG_REPORT",
                Name = "Bug Report",
                IsActive = true
            },
            new TicketCategory
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"),
                Code = "FEATURE_REQUEST",
                Name = "Feature Request",
                IsActive = true
            },
            new TicketCategory
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000006"),
                Code = "GENERAL_ENQUIRY",
                Name = "General Enquiry",
                IsActive = true
            });
    }
}