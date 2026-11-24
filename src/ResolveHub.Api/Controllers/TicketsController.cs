using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Models.Tickets;
using ResolveHub.Domain.Entities;
using ResolveHub.Domain.Enums;
using ResolveHub.Infrastructure.Data;
using ResolveHub.Infrastructure.Services;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TicketCreationService _ticketCreationService;

    public TicketsController(
        ApplicationDbContext dbContext,
        TicketCreationService ticketCreationService)
    {
        _dbContext = dbContext;
        _ticketCreationService = ticketCreationService;
    }

    [HttpGet]
    [Authorize(Policy = "Tickets.Read")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TicketResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Tickets.AsNoTracking();

        if (User.IsInRole("Customer"))
        {
            if (!TryGetCustomerOrganisationId(out var organisationId))
            {
                return Forbid();
            }

            query = query.Where(ticket =>
                ticket.OrganisationId == organisationId);
        }

        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(tickets.Select(ToTicketResponse).ToList());
    }

    [HttpGet("{ticketId:guid}")]
    [Authorize(Policy = "Tickets.Read")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> GetById(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Tickets.AsNoTracking();

        if (User.IsInRole("Customer"))
        {
            if (!TryGetCustomerOrganisationId(out var organisationId))
            {
                return Forbid();
            }

            query = query.Where(ticket =>
                ticket.OrganisationId == organisationId);
        }

        var ticket = await query
            .Where(ticket => ticket.Id == ticketId)
            .SingleOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        return Ok(ToTicketResponse(ticket));
    }

    [HttpGet("{ticketId:guid}/comments")]
    [Authorize(Policy = "Tickets.Read")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketCommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketCommentResponse>>> GetComments(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var tickets = _dbContext.Tickets.AsNoTracking();

        if (User.IsInRole("Customer"))
        {
            if (!TryGetCustomerOrganisationId(out var organisationId))
            {
                return Forbid();
            }

            tickets = tickets.Where(ticket =>
                ticket.OrganisationId == organisationId);
        }

        var ticketExists = await tickets.AnyAsync(
            ticket => ticket.Id == ticketId,
            cancellationToken);

        if (!ticketExists)
        {
            return NotFound();
        }

        var comments = _dbContext.TicketComments
            .AsNoTracking()
            .Where(comment => comment.TicketId == ticketId);

        if (User.IsInRole("Customer"))
        {
            comments = comments.Where(comment =>
                comment.Visibility == CommentVisibility.CustomerVisible);
        }

        var responses = await comments
            .OrderBy(comment => comment.CreatedAtUtc)
            .Select(comment => new TicketCommentResponse
            {
                Id = comment.Id,
                TicketId = comment.TicketId,
                AuthorId = comment.AuthorId,
                Content = comment.Content,
                Visibility = comment.Visibility,
                CreatedAtUtc = comment.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(responses);
    }

    [HttpPost("{ticketId:guid}/comments")]
    [Authorize(Policy = "Tickets.Comment")]
    [ProducesResponseType(typeof(TicketCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketCommentResponse>> AddComment(
        Guid ticketId,
        [FromBody] AddTicketCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var authorId))
        {
            return Forbid();
        }

        var tickets = _dbContext.Tickets.AsNoTracking();

        if (User.IsInRole("Customer"))
        {
            if (!TryGetCustomerOrganisationId(out var organisationId))
            {
                return Forbid();
            }

            tickets = tickets.Where(ticket =>
                ticket.OrganisationId == organisationId);
        }

        var ticketExists = await tickets.AnyAsync(
            ticket => ticket.Id == ticketId,
            cancellationToken);

        if (!ticketExists)
        {
            return NotFound();
        }

        var comment = new TicketComment
        {
            TicketId = ticketId,
            AuthorId = authorId,
            Content = request.Content,
            Visibility = CommentVisibility.CustomerVisible,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.TicketComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new TicketCommentResponse
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            AuthorId = comment.AuthorId,
            Content = comment.Content,
            Visibility = comment.Visibility,
            CreatedAtUtc = comment.CreatedAtUtc
        };

        return CreatedAtAction(
            nameof(GetComments),
            new { ticketId },
            response);
    }

    [HttpPost]
    [Authorize(Policy = "Tickets.Create")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketResponse>> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var organisationId = request.OrganisationId;
        var requesterId = request.RequesterId;

        if (User.IsInRole("Customer"))
        {
            if (!Guid.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out requesterId)
                || !TryGetCustomerOrganisationId(out organisationId))
            {
                return Forbid();
            }
        }

        try
        {
            var ticket = await _ticketCreationService.CreateAsync(
                request.Subject,
                request.Description,
                request.CategoryId,
                request.Priority,
                organisationId,
                requesterId,
                cancellationToken);

            var response = ToTicketResponse(ticket);

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

    private bool TryGetCustomerOrganisationId(out Guid organisationId)
    {
        return Guid.TryParse(
            User.FindFirst("organisation_id")?.Value,
            out organisationId);
    }

    private static TicketResponse ToTicketResponse(Ticket ticket)
    {
        return new TicketResponse
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
    }
}
