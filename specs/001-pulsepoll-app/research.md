# Research: PulsePoll Technology Decisions

**Feature**: PulsePoll - Conference Polling App  
**Phase**: 0 - Outline & Research  
**Date**: 2026-01-06

## Purpose

Document technology choices, architectural decisions, and best practices for building a real-time conference polling application with .NET 10 in GitHub Codespaces.

## Technology Stack Decisions

### 1. Web Framework: ASP.NET Core 10 MVC + Razor Pages

**Decision**: Use hybrid MVC + Razor Pages approach
- **MVC Controllers**: Host dashboard (authenticated endpoints)
- **Razor Pages**: Participant flows (voting, results viewing)

**Rationale**:
- MVC controllers ideal for API-like host actions (create poll, close poll)
- Razor Pages better for content-focused participant pages (voting form, results display)
- Both share same authentication, middleware, and DI container
- Avoids complexity of separate frontend/backend projects

**Alternatives Considered**:
- Blazor Server: More interactive but adds complexity, SignalR already used for real-time updates
- Blazor WebAssembly: Requires separate API project, increases bundle size, unnecessary for form-based voting
- Minimal APIs: Less structure for a UI-heavy application

**Best Practices**:
- Use MVVM pattern in Razor Pages (PageModel separation)
- Keep controllers thin, business logic in services
- Use middleware for cross-cutting concerns (auth, logging)

### 2. Real-Time Updates: SignalR Core

**Decision**: SignalR for broadcasting vote updates to results page with JavaScript client fallback to polling

**Rationale**:
- Native .NET integration, works with ASP.NET Core hosting
- Handles WebSocket fallback automatically (Server-Sent Events, long polling)
- Broadcast pattern ideal for one-to-many updates (votes → all results viewers)
- Minimal client-side code with @microsoft/signalr npm package

**Alternatives Considered**:
- Server-Sent Events (SSE): Simpler but one-way only, no acknowledgments
- Polling only: Higher latency, more server load, less responsive
- WebSockets directly: SignalR provides abstraction and fallbacks

**Best Practices**:
- Hub per feature (ResultsHub for vote broadcasting)
- Client reconnection logic with exponential backoff
- Throttle broadcasts during rapid voting (max 1 update/second)
- Fallback to periodic polling if SignalR connection fails (5s intervals)

### 3. Database: SQLite with Entity Framework Core

**Decision**: SQLite with EF Core 10, code-first migrations

**Rationale**:
- Single-file database, zero configuration in Codespaces
- Sufficient for conference scale (100 participants, <10 polls)
- EF Core provides LINQ queries, migrations, and change tracking
- Data loss on container restart acceptable per spec assumptions

**Alternatives Considered**:
- PostgreSQL: Overkill for ephemeral data, requires Docker Compose in Codespaces
- In-memory collections: No persistence across restarts, loses data on crash
- Azure SQL: Requires cloud resources, violates zero-deployment constraint

**Best Practices**:
- Index on `Poll.Code` (primary lookup pattern)
- Index on `Vote.VoterId` for duplicate prevention queries
- Use transactions for vote updates (delete old vote + insert new vote)
- Configure cascade deletes (deleting poll deletes options and votes)

### 4. QR Code Generation: QRCoder NuGet Package

**Decision**: Use QRCoder library for generating QR code images

**Rationale**:
- Pure C# implementation, no native dependencies
- Generates PNG/SVG/Base64 formats
- MIT license, actively maintained
- Simple API: `QRCodeGenerator.CreateQrCode(url).GetGraphic(20)`

**Alternatives Considered**:
- ZXing.Net: Heavier library, includes scanning (not needed)
- External API (qrcode.com): Network dependency, rate limits, privacy concerns
- JavaScript generation (qrcodejs): Client-side, doesn't work for printed codes

**Best Practices**:
- Cache generated QR codes per poll (store as Base64 in database or memory cache)
- Regenerate only when Codespaces public URL changes
- Use appropriate size for projection (200x200 minimum per UX-009)
- Include error correction level Medium for better scanning

### 5. Authentication: Simple Token Middleware

**Decision**: Custom middleware validating host token from environment variable or cookie

**Rationale**:
- Conference demo scope doesn't require user accounts
- Token from `appsettings.json` or environment variable `HOST_TOKEN`
- Middleware checks `X-Host-Token` header or `HostAuth` cookie
- Participants require no authentication

**Alternatives Considered**:
- ASP.NET Core Identity: Too complex for single-user scenario
- JWT tokens: Unnecessary signing/validation overhead
- Basic Auth: Less flexible, harder to use with cookie-based session

**Best Practices**:
- Use Data Protection API to encrypt cookie value
- Set `HttpOnly`, `Secure` flags on host cookie
- Short token (8-16 chars) for manual entry if needed
- Return 401 with clear message on auth failure (per FR-025)

### 6. Voter Identity Tracking: HTTP Cookies

**Decision**: Generate GUID on first vote, store in persistent cookie

**Rationale**:
- No user accounts or login required
- Cookie persists across page refreshes
- Simple duplicate prevention: check for existing vote by VoterId
- Meets spec requirement FR-005 (cookie or session-based)

**Alternatives Considered**:
- Session storage: Lost on browser close, less persistent
- Fingerprinting: Privacy concerns, less reliable
- LocalStorage: Requires JavaScript, cookies work server-side

**Best Practices**:
- Cookie name: `PulsePollVoterId`
- Expiration: 7 days (covers multi-day conferences)
- SameSite=Lax for security
- Generate GUID on first vote submission, not page load

## Architecture Decisions

### 7. Responsive Design: Mobile-First CSS

**Decision**: Bootstrap 5 with custom CSS for projection mode

**Rationale**:
- Participants vote from phones (mobile-first critical)
- Results page needs large fonts for projection (custom CSS)
- Bootstrap provides responsive grid, components, accessibility

**Best Practices**:
- Breakpoints: Mobile (<768px), Tablet (768-1024px), Desktop/Projection (>1024px)
- Results page: minimum 24px font (per UX-007 and PERF-017)
- High contrast mode for projection (dark background, bright text)
- Touch-friendly targets (min 44x44px per WCAG)

### 8. Error Handling: Global Exception Handler + User-Friendly Messages

**Decision**: Custom middleware for exceptions, return actionable error pages

**Rationale**:
- Constitution Principle III: actionable error messages
- Catch 404 (poll not found), 401 (auth), 500 (server errors)
- Log errors server-side, display friendly messages client-side

**Best Practices**:
- Custom error pages: 404.cshtml, 401.cshtml, 500.cshtml
- Error messages include recovery actions (e.g., "Check the poll code and try again")
- Log structured errors (Serilog with context: poll code, voter ID, action)

### 9. Performance Optimization

**Decision**: Implement caching, connection pooling, and query optimization

**Rationale**:
- Meet performance requirements (PERF-001 to PERF-010)
- 100+ concurrent voters need efficient data access

**Best Practices**:
- **Caching**: Memory cache for active poll (reduce DB queries), cache QR codes
- **Indexing**: Poll.Code, Vote.VoterId, Vote.PollId indexes
- **Connection pooling**: EF Core default pooling (max 100 connections)
- **SignalR scaling**: In-memory backplane sufficient for single Codespaces instance
- **Throttling**: Batch SignalR updates during rapid votes (max 1/second per PERF-009)

### 10. GitHub Codespaces Configuration

**Decision**: Use .devcontainer for .NET 10 SDK, auto-forward port 5000

**Rationale**:
- Consistent development environment
- Auto-install .NET 10 SDK, SQLite tools
- Port forwarding visibility set to Public for participant access

**Best Practices**:
- `.devcontainer/devcontainer.json`:
  - Image: `mcr.microsoft.com/devcontainers/dotnet:10`
  - Forward port 5000 (HTTP), set visibility: public
  - Install extensions: C# Dev Kit, SQLite Viewer
- `launchSettings.json`: Configure Kestrel to listen on `http://0.0.0.0:5000`
- Document Codespaces URL retrieval: Ports tab → copy public URL

## Integration Points

### 11. SignalR + Razor Pages Integration

**Pattern**: Hub invocation from VoteService, JavaScript client in Results.cshtml

```
Vote Submission Flow:
1. POST /p/{code}/vote → VoteService.SubmitVote()
2. VoteService saves to DB
3. VoteService calls ResultsHub.Clients.Group(code).SendAsync("VoteUpdated", results)
4. Results.cshtml JS client receives update, re-renders vote counts
5. Fallback: setInterval() polling /api/results/{code} every 5s
```

**Best Practices**:
- Group results viewers by poll code (one group per poll)
- Client joins group on page load: `connection.invoke("JoinPollGroup", pollCode)`
- Server-side: `Groups.AddToGroupAsync(Context.ConnectionId, pollCode)`

## Testing Strategy

### 12. TDD with xUnit, FluentAssertions, Moq

**Decision**: Write tests first (Red-Green-Refactor), 80% coverage minimum

**Test Categories**:
- **Unit Tests**: Services (PollService, VoteService), isolated with Moq
- **Integration Tests**: WebApplicationFactory for end-to-end flows (create poll → vote → view results)
- **Contract Tests**: API endpoints, validate request/response schemas

**Best Practices**:
- Arrange-Act-Assert pattern
- One assertion per test (FluentAssertions for readability)
- Use InMemory SQLite for integration tests (isolated DB per test)
- Test duplicate vote prevention explicitly (critical path = 100% coverage)

## Deployment & Operations

### 13. Running in Codespaces

**Decision**: `dotnet watch run` for development, no Docker/deployment

**Workflow**:
1. Open Codespaces
2. Terminal: `dotnet run --project PulsePoll` or `dotnet watch run`
3. Ports tab: Set port 5000 visibility to Public
4. Copy public URL (e.g., `https://super-disco-abc123.app.github.dev`)
5. Share URL or QR code with participants

**Best Practices**:
- Use `dotnet watch` for hot reload during development
- Configure `appsettings.Development.json` for Codespaces-specific settings
- Document URL copying steps in quickstart.md

## Open Questions → Decisions

All technical unknowns from plan.md Technical Context have been researched and resolved:

| Question | Decision |
|----------|----------|
| Code generation strategy? | Random 4-char alphanumeric, retry on collision (low probability with 1.6M combinations) |
| Auto-close mechanism? | PollService.CreatePoll() sets IsClosed=true on previous active poll before creating new |
| Voter ID lifecycle? | 7-day cookie expiration, regenerate on cookie loss (acceptable re-vote) |
| Results aggregation? | LINQ GroupBy query: `Votes.GroupBy(v => v.OptionId).Select(g => new { OptionId = g.Key, Count = g.Count() })` |
| SignalR scaling? | Single Codespaces instance, in-memory backplane, no Redis needed for conference scale |
| Codespaces URL discovery? | Environment variable or programmatic via Codespaces API (fallback: manual copy from Ports tab) |

## References

- [ASP.NET Core Best Practices](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices)
- [SignalR Performance](https://learn.microsoft.com/aspnet/core/signalr/scale)
- [EF Core Performance](https://learn.microsoft.com/ef/core/performance/)
- [WCAG 2.1 AA Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [.NET Testing Best Practices](https://learn.microsoft.com/dotnet/core/testing/best-practices)
