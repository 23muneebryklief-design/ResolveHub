
using Microsoft.EntityFrameworkCore;
using ResolveHub.Domain.Entities;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Infrastructure.Services;

public class TicketHistoryService
{
    private readonly ApplicationDbContext _dbContext;

    public TicketHistoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TicketHistory>> GetByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TicketHistory
            .AsNoTracking()
            .Where(history => history.TicketId == ticketId)
            .Include(history => history.Actor)
            .OrderBy(history => history.CreatedAtUtc)
            .ThenBy(history => history.Id)
            .ToListAsync(cancellationToken);
    }
}
