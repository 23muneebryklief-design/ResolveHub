
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Models.Tickets;
using ResolveHub.Infrastructure.Services;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly TicketCreationService _ticketCreationService;

    public TicketsController(TicketCreationService ticketCreationService)
    {
        _ticketCreationService = ticketCreationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketResponse>> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await _ticketCreationService.CreateAsync(
                request.Subject,
                request.Description,
                request.CategoryId,
                request.Priority,
                request.OrganisationId,
                request.RequesterId,
                cancellationToken);

            var response = new TicketResponse
            {
                Id = ticket.Id,
                Reference = ticket.Reference,
                Subject = ticket.Subject,
                Description = ticket.Description,
                CategoryId = ticket.CategoryId,
                Priority = ticket.Priority,
                Status = ticket.Status,
                OrganisationId = ticket.OrganisationId,
                RequesterId = ticket.RequesterId,
                CreatedAtUtc = ticket.CreatedAtUtc
            };

            return StatusCode(
                StatusCodes.Status201Created,
                response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }
}
