
using Microsoft.EntityFrameworkCore;
using ResolveHub.Infrastructure.Data;
using ResolveHub.Infrastructure.Services;

namespace ResolveHub.Tests;

public class TicketReferenceGeneratorTests
{
    private const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=ResolveHubDb_Tests;Trusted_Connection=True;MultipleActiveResultSets=True";

    [Fact]
    public async Task GenerateAsync_ShouldReturnUniqueReferences_WhenCalledConcurrently()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        // Apply migrations to the isolated test database.
        await using (var setupContext = new ApplicationDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
        }

        // Generate references sequentially.
        var sequentialReferences = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            await using var context = new ApplicationDbContext(options);
            var generator = new TicketReferenceGenerator(context);

            sequentialReferences.Add(
                await generator.GenerateAsync());
        }

        // Generate references concurrently, using a separate DbContext
        // for each operation.
        var concurrentTasks = Enumerable.Range(0, 50)
            .Select(async _ =>
            {
                await using var context = new ApplicationDbContext(options);
                var generator = new TicketReferenceGenerator(context);

                return await generator.GenerateAsync();
            });

        var concurrentReferences = await Task.WhenAll(concurrentTasks);

        var allReferences = sequentialReferences
            .Concat(concurrentReferences)
            .ToList();

        // Every reference must have the expected format.
        Assert.All(allReferences, reference =>
            Assert.Matches(@"^TKT-\d{6,}$", reference));

        // Every generated reference must be unique.
        Assert.Equal(
            allReferences.Count,
            allReferences.Distinct().Count());
    }
}
