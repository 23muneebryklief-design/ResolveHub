# ResolveHub API Authorization Matrix

The API enforces access through JWT bearer authentication and server-side
authorization policies. Role claims are issued from the authenticated user's
stored `UserRole`. Customer queries and comments are scoped to the organisation
claim in that signed token.

| Endpoint | Anonymous | Customer | SupportAgent | Administrator | OperationsManager |
| --- | --- | --- | --- | --- | --- |
| `POST /api/auth/login` | Allowed | Allowed | Allowed | Allowed | Allowed |
| `GET /health` | Allowed | Allowed | Allowed | Allowed | Allowed |
| `GET /api/tickets` | Denied (`401`) | Own organisation | All organisations | All organisations | Denied (`403`) |
| `GET /api/tickets/{ticketId}` | Denied (`401`) | Own organisation | All organisations | All organisations | Denied (`403`) |
| `GET /api/tickets/{ticketId}/comments` | Denied (`401`) | Own organisation, customer-visible comments | All organisations | All organisations | Denied (`403`) |
| `POST /api/tickets/{ticketId}/comments` | Denied (`401`) | Own organisation | All organisations | All organisations | Denied (`403`) |
| `POST /api/tickets` | Denied (`401`) | Own organisation and identity | All organisations | All organisations | Denied (`403`) |

Unauthenticated protected requests receive `401 Unauthorized`. Authenticated
requests with roles outside a policy receive `403 Forbidden`. A customer who
requests a ticket outside their organisation receives `404 Not Found` so the
API does not disclose whether that ticket exists.
