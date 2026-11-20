namespace ResolveHub.Domain.Enums;

public enum TicketHistoryEventType
{
    TicketCreated = 1,
    StatusChanged = 2,
    AssignmentChanged = 3,
    PriorityChanged = 4,
    CommentAdded = 5,
    Resolved = 6,
    Closed = 7,
    Reopened = 8
}
