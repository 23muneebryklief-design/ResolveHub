
using Microsoft.EntityFrameworkCore;
using ResolveHub.Domain.Entities;
using ResolveHub.Domain.Enums;
using ResolveHub.Domain.Interfaces;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Infrastructure.Services;

public class TicketCreationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITicketReferenceGenerator _referenceGenerator;

    public TicketCreationService(
        ApplicationDbContext dbContext,
        ITicketReferenceGenerator referenceGenerator)
    {
        _dbContext = dbContext;
        _referenceGenerator = referenceGenerator;
    }

    public async Task<Ticket> CreateAsync(
        string subject,
        string description,
        Guid categoryId,
        TicketPriority priority,
        Guid organisationId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var categoryExists = await _dbContext.TicketCategories
            .AnyAsync(
                x => x.Id == categoryId && x.IsActive,
                cancellationToken);

        if (!categoryExists)
        {
            throw new ArgumentException(
                "The selected ticket category does not exist or is inactive.",
                nameof(categoryId));
        }

        var organisationExists = await _dbContext.Organisations
            .AnyAsync(
                x => x.Id == organisationId,
                cancellationToken);

        if (!organisationExists)
        {
            throw new ArgumentException(
                "The selected organisation does not exist.",
                nameof(organisationId));
        }

        var requesterExists = await _dbContext.Users
            .AnyAsync(
                x => x.Id == requesterId,
                cancellationToken);

        if (!requesterExists)
        {
            throw new ArgumentException(
                "The selected requester does not exist.",
                nameof(requesterId));
        }

        if (!Enum.IsDefined(typeof(TicketPriority), priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        var now = DateTimeOffset.UtcNow;

        var ticket = new Ticket
        {
            Reference = await _referenceGenerator.GenerateAsync(
                cancellationToken),
            Subject = subject,
            Description = description,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.Open,
            OrganisationId = organisationId,
            RequesterId = requesterId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Tickets.Add(ticket);

        _dbContext.TicketHistory.Add(new TicketHistory
        {
            Ticket = ticket,
            ActorId = requesterId,
            EventType = TicketHistoryEventType.TicketCreated,
            PreviousValue = null,
            NewValue = ticket.Status.ToString(),
            CreatedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ticket;
    }
}
