
using System.Data;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Domain.Interfaces;
using ResolveHub.Infrastructure.Data;

namespace ResolveHub.Infrastructure.Services;

public class TicketReferenceGenerator : ITicketReferenceGenerator
{
    private readonly ApplicationDbContext _dbContext;

    public TicketReferenceGenerator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        try
        {
            if (!wasOpen)
            {
                await _dbContext.Database.OpenConnectionAsync(
                    cancellationToken);
            }

            using var command = connection.CreateCommand();

            command.CommandText =
                "SELECT NEXT VALUE FOR dbo.TicketReferenceSequence;";

            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            var nextNumber = Convert.ToInt64(result);

            return $"TKT-{nextNumber:D6}";
        }
        finally
        {
            if (!wasOpen)
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
