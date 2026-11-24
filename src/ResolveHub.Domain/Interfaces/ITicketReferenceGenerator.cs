namespace ResolveHub.Domain.Interfaces;

public interface ITicketReferenceGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}