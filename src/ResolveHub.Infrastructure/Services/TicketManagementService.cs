
using Microsoft.EntityFrameworkCore;
using ResolveHub.Domain.Entities;
using ResolveHub.Domain.Enums;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Infrastructure.Services;

public class TicketManagementService
{
    private readonly ApplicationDbContext _dbContext;

    public TicketManagementService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Ticket?> AssignAsync(
        Guid ticketId,
        Guid assigneeId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetPermittedActorAsync(
            actorId,
            cancellationToken);

        var ticket = await FindAccessibleTicketAsync(
            ticketId,
            actor,
            cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var assignee = await _dbContext.Users
            .SingleOrDefaultAsync(
                user => user.Id == assigneeId
                    && user.IsActive
                    && user.Role == UserRole.SupportAgent
                    && user.OrganisationId == ticket.OrganisationId,
                cancellationToken);

        if (assignee is null)
        {
            throw new InvalidOperationException(
                "The assignee must be an active support agent " +
                "belonging to the ticket's organisation.");
        }

        if (ticket.AssigneeId == assigneeId)
        {
            throw new InvalidOperationException(
                "The ticket is already assigned to this support agent.");
        }

        var now = DateTimeOffset.UtcNow;
        var previousAssigneeId = ticket.AssigneeId;

        ticket.AssigneeId = assigneeId;
        ticket.UpdatedAtUtc = now;

        _dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            ActorId = actorId,
            EventType = TicketHistoryEventType.AssignmentChanged,
            PreviousValue = previousAssigneeId?.ToString(),
            NewValue = assigneeId.ToString(),
            CreatedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ticket;
    }

    public async Task<Ticket?> UpdateStatusAsync(
        Guid ticketId,
        TicketStatus newStatus,
        string? resolutionNotes,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(TicketStatus), newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus));
        }

        var actor = await GetPermittedActorAsync(
            actorId,
            cancellationToken);

        var ticket = await FindAccessibleTicketAsync(
            ticketId,
            actor,
            cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        if (!IsAllowedTransition(ticket.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"The ticket cannot be moved from {ticket.Status} " +
                $"to {newStatus}.");
        }

        if (newStatus == TicketStatus.Resolved
            && string.IsNullOrWhiteSpace(resolutionNotes))
        {
            throw new InvalidOperationException(
                "Resolution notes are required when resolving a ticket.");
        }

        var now = DateTimeOffset.UtcNow;
        var previousStatus = ticket.Status;

        ticket.Status = newStatus;
        ticket.UpdatedAtUtc = now;

        if (newStatus == TicketStatus.Resolved)
        {
            ticket.ResolutionNotes = resolutionNotes!.Trim();
            ticket.ResolvedAtUtc = now;
        }
        else if (newStatus == TicketStatus.Closed)
        {
            ticket.ClosedAtUtc = now;
        }
        else if (newStatus == TicketStatus.Open)
        {
            ticket.ResolutionNotes = null;
            ticket.ResolvedAtUtc = null;
            ticket.ClosedAtUtc = null;
        }

        var eventType = newStatus switch
        {
            TicketStatus.Resolved => TicketHistoryEventType.Resolved,
            TicketStatus.Closed => TicketHistoryEventType.Closed,
            TicketStatus.Open when
                previousStatus == TicketStatus.Resolved
                || previousStatus == TicketStatus.Closed
                    => TicketHistoryEventType.Reopened,
            _ => TicketHistoryEventType.StatusChanged
        };

        _dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            ActorId = actorId,
            EventType = eventType,
            PreviousValue = previousStatus.ToString(),
            NewValue = newStatus.ToString(),
            CreatedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ticket;
    }

    private async Task<ApplicationUser> GetPermittedActorAsync(
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var actor = await _dbContext.Users
            .SingleOrDefaultAsync(
                user => user.Id == actorId && user.IsActive,
                cancellationToken);

        if (actor is null
            || (actor.Role != UserRole.SupportAgent
                && actor.Role != UserRole.Administrator))
        {
            throw new UnauthorizedAccessException(
                "You are not permitted to manage tickets.");
        }

        if (actor.Role == UserRole.SupportAgent
            && !actor.OrganisationId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Your account is not assigned to an organisation.");
        }

        return actor;
    }

    private async Task<Ticket?> FindAccessibleTicketAsync(
        Guid ticketId,
        ApplicationUser actor,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Tickets.AsQueryable();

        if (actor.Role == UserRole.SupportAgent)
        {
            query = query.Where(ticket =>
                ticket.OrganisationId == actor.OrganisationId!.Value);
        }

        return await query.SingleOrDefaultAsync(
            ticket => ticket.Id == ticketId,
            cancellationToken);
    }

    private static bool IsAllowedTransition(
        TicketStatus currentStatus,
        TicketStatus newStatus)
    {
        return (currentStatus, newStatus) switch
        {
            (TicketStatus.Open, TicketStatus.InProgress) => true,

            (TicketStatus.InProgress, TicketStatus.Resolved) => true,

            (TicketStatus.Resolved, TicketStatus.Closed) => true,

            (TicketStatus.Resolved, TicketStatus.Open) => true,

            (TicketStatus.Closed, TicketStatus.Open) => true,

            _ => false
        };
    }
}
