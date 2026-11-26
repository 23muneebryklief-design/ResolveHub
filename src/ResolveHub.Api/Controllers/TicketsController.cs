
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
    private readonly TicketManagementService _ticketManagementService;
    private readonly TicketHistoryService _ticketHistoryService;

    public TicketsController(
        ApplicationDbContext dbContext,
        TicketCreationService ticketCreationService,
        TicketManagementService ticketManagementService,
        TicketHistoryService ticketHistoryService)
    {
        _dbContext = dbContext;
        _ticketCreationService = ticketCreationService;
        _ticketManagementService = ticketManagementService;
        _ticketHistoryService = ticketHistoryService;
    }

    [HttpGet]
    [Authorize(Policy = "Tickets.Read")]
    [ProducesResponseType(typeof(TicketListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketListResponse>> GetAll(
        [FromQuery] TicketListQuery filters,
        CancellationToken cancellationToken)
    {
        if (!TryGetTicketOrganisationScope(out var organisationId))
        {
            return Forbid();
        }

        var query = BuildTicketQuery();

        if (organisationId.HasValue)
        {
            query = query.Where(ticket =>
                ticket.OrganisationId == organisationId.Value);
        }

        if (filters.Status.HasValue)
        {
            query = query.Where(ticket =>
                ticket.Status == filters.Status.Value);
        }

        if (filters.Priority.HasValue)
        {
            query = query.Where(ticket =>
                ticket.Priority == filters.Priority.Value);
        }

        if (filters.CategoryId.HasValue)
        {
            query = query.Where(ticket =>
                ticket.CategoryId == filters.CategoryId.Value);
        }

        if (filters.AssigneeId.HasValue)
        {
            query = query.Where(ticket =>
                ticket.AssigneeId == filters.AssigneeId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancellationToken);

        return Ok(new TicketListResponse
        {
            Items = tickets.Select(ToTicketResponse).ToList(),
            Page = filters.Page,
            PageSize = filters.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)filters.PageSize)
        });
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
        if (!TryGetTicketOrganisationScope(out var organisationId))
        {
            return Forbid();
        }

        var query = BuildTicketQuery();

        if (organisationId.HasValue)
        {
            query = query.Where(ticket =>
                ticket.OrganisationId == organisationId.Value);
        }

        var ticket = await query
            .SingleOrDefaultAsync(
                item => item.Id == ticketId,
                cancellationToken);

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
        if (!TryGetTicketOrganisationScope(out var organisationId))
        {
            return Forbid();
        }

        var tickets = _dbContext.Tickets.AsNoTracking();

        if (organisationId.HasValue)
        {
            tickets = tickets.Where(ticket =>
                ticket.OrganisationId == organisationId.Value);
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

    [HttpGet("{ticketId:guid}/history")]
    [Authorize(Policy = "Tickets.Read")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketHistoryResponse>>> GetHistory(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        if (!TryGetTicketOrganisationScope(out var organisationId))
        {
            return Forbid();
        }

        var tickets = _dbContext.Tickets.AsNoTracking();

        if (organisationId.HasValue)
        {
            tickets = tickets.Where(ticket =>
                ticket.OrganisationId == organisationId.Value);
        }

        var ticketExists = await tickets.AnyAsync(
            ticket => ticket.Id == ticketId,
            cancellationToken);

        if (!ticketExists)
        {
            return NotFound();
        }

        var history = await _ticketHistoryService.GetByTicketIdAsync(
            ticketId,
            cancellationToken);

        var responses = history.Select(entry => new TicketHistoryResponse
        {
            Id = entry.Id,
            TicketId = entry.TicketId,
            ActorId = entry.ActorId,
            Actor = new TicketUserSummaryResponse
            {
                Id = entry.Actor.Id,
                FirstName = entry.Actor.FirstName,
                LastName = entry.Actor.LastName
            },
            EventType = entry.EventType,
            PreviousValue = entry.PreviousValue,
            NewValue = entry.NewValue,
            CreatedAtUtc = entry.CreatedAtUtc
        }).ToList();

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

        if (!TryGetTicketOrganisationScope(out var organisationId))
        {
            return Forbid();
        }

        var tickets = _dbContext.Tickets.AsNoTracking();

        if (organisationId.HasValue)
        {
            tickets = tickets.Where(ticket =>
                ticket.OrganisationId == organisationId.Value);
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
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var requesterId))
        {
            return Forbid();
        }

        var authenticatedUser = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == requesterId && user.IsActive)
            .Select(user => new
            {
                user.Id,
                user.OrganisationId,
                user.Role
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (authenticatedUser is null)
        {
            return Forbid();
        }

        if (authenticatedUser.Role != UserRole.Customer
            && authenticatedUser.Role != UserRole.SupportAgent
            && authenticatedUser.Role != UserRole.Administrator)
        {
            return Forbid();
        }

        if (!authenticatedUser.OrganisationId.HasValue)
        {
            return Forbid();
        }

        var organisationId = authenticatedUser.OrganisationId.Value;

        var organisationIsActive = await _dbContext.Organisations
            .AnyAsync(
                organisation => organisation.Id == organisationId
                    && organisation.IsActive,
                cancellationToken);

        if (!organisationIsActive)
        {
            return Forbid();
        }

        try
        {
            var ticket = await _ticketCreationService.CreateAsync(
                request.Subject.Trim(),
                request.Description.Trim(),
                request.CategoryId,
                request.Priority,
                organisationId,
                requesterId,
                cancellationToken);

            var createdTicket = await BuildTicketQuery()
                .SingleAsync(
                    item => item.Id == ticket.Id,
                    cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                ToTicketResponse(createdTicket));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }

    [HttpPost("{ticketId:guid}/assign")]
    [Authorize(Policy = "Tickets.Assign")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Assign(
        Guid ticketId,
        [FromBody] AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var actorId))
        {
            return Forbid();
        }

        try
        {
            var ticket = await _ticketManagementService.AssignAsync(
                ticketId,
                request.AssigneeId,
                actorId,
                cancellationToken);

            if (ticket is null)
            {
                return NotFound();
            }

            var updatedTicket = await BuildTicketQuery()
                .SingleAsync(
                    item => item.Id == ticketId,
                    cancellationToken);

            return Ok(ToTicketResponse(updatedTicket));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }

    [HttpPatch("{ticketId:guid}/status")]
    [Authorize(Policy = "Tickets.UpdateStatus")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> UpdateStatus(
        Guid ticketId,
        [FromBody] UpdateTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var actorId))
        {
            return Forbid();
        }

        try
        {
            var ticket = await _ticketManagementService.UpdateStatusAsync(
                ticketId,
                request.Status,
                request.ResolutionNotes,
                actorId,
                cancellationToken);

            if (ticket is null)
            {
                return NotFound();
            }

            var updatedTicket = await BuildTicketQuery()
                .SingleAsync(
                    item => item.Id == ticketId,
                    cancellationToken);

            return Ok(ToTicketResponse(updatedTicket));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }

    [HttpPatch("{ticketId:guid}/priority")]
    [Authorize(Policy = "Tickets.UpdatePriority")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> UpdatePriority(
        Guid ticketId,
        [FromBody] UpdateTicketPriorityRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var actorId))
        {
            return Forbid();
        }

        try
        {
            var ticket = await _ticketManagementService.UpdatePriorityAsync(
                ticketId,
                request.Priority,
                actorId,
                cancellationToken);

            if (ticket is null)
            {
                return NotFound();
            }

            var updatedTicket = await BuildTicketQuery()
                .SingleAsync(
                    item => item.Id == ticketId,
                    cancellationToken);

            return Ok(ToTicketResponse(updatedTicket));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                error = exception.Message
            });
        }
    }

    private IQueryable<Ticket> BuildTicketQuery()
    {
        return _dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.Requester)
            .Include(ticket => ticket.Assignee);
    }

    private bool TryGetTicketOrganisationScope(
        out Guid? organisationId)
    {
        organisationId = null;

        if (User.IsInRole("Customer")
            || User.IsInRole("SupportAgent"))
        {
            if (!Guid.TryParse(
                    User.FindFirst("organisation_id")?.Value,
                    out var parsedOrganisationId)
                || parsedOrganisationId == Guid.Empty)
            {
                return false;
            }

            organisationId = parsedOrganisationId;
        }

        return true;
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
            CategoryName = ticket.Category.Name,
            Priority = ticket.Priority,
            Status = ticket.Status,
            OrganisationId = ticket.OrganisationId,
            RequesterId = ticket.RequesterId,
            Requester = new TicketUserSummaryResponse
            {
                Id = ticket.Requester.Id,
                FirstName = ticket.Requester.FirstName,
                LastName = ticket.Requester.LastName
            },
            AssigneeId = ticket.AssigneeId,
            Assignee = ticket.Assignee is null
                ? null
                : new TicketUserSummaryResponse
                {
                    Id = ticket.Assignee.Id,
                    FirstName = ticket.Assignee.FirstName,
                    LastName = ticket.Assignee.LastName
                },
            CreatedAtUtc = ticket.CreatedAtUtc,
            UpdatedAtUtc = ticket.UpdatedAtUtc
        };
    }
}
