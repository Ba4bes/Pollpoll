# Implementation Plan: PulsePoll - Conference Polling App

**Branch**: `001-pulsepoll-app` | **Date**: 2026-01-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-pulsepoll-app/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

PulsePoll is a real-time conference polling web application that enables hosts to create polls, share join codes/QR codes with audience members, and display live voting results on a projector. Built with .NET 10 (ASP.NET Core + Blazor), it runs in GitHub Codespaces with public port forwarding for zero-deployment conference demos. The app supports single/multi-choice polls, prevents duplicate voting via browser cookies, and provides SignalR-powered live result updates with periodic polling fallback. SQLite provides lightweight persistence suitable for conference room scale (100 participants).

## Technical Context

**Language/Version**: .NET 10 (C# 13), ASP.NET Core 10.0  
**Primary Dependencies**: 
- ASP.NET Core MVC/Razor Pages (host dashboard, voting pages)
- Blazor Server (optional for results page live updates)
- SignalR Core (real-time vote updates to results display)
- Entity Framework Core 10 (SQLite provider for data persistence)
- QRCoder or similar NuGet package (QR code generation)
- xUnit (unit and integration testing framework)

**Storage**: SQLite database (single-file, embedded, suitable for Codespaces ephemeral storage)  
**Testing**: xUnit with FluentAssertions, Moq for mocking, WebApplicationFactory for integration tests  
**Target Platform**: GitHub Codespaces (Linux container), accessible via public port forwarding  
**Project Type**: Web application (single ASP.NET Core project, server-side rendering with optional Blazor components)  
**Performance Goals**: 
- Poll creation <500ms (spec: PERF-001)
- Vote submission <300ms p95 (spec: PERF-002)
- Results page load <2s on 3G (spec: PERF-003)
- SignalR updates <2s latency (spec: PERF-004)
- Handle 100+ concurrent voters (spec: PERF-007)

**Constraints**: 
- Zero-deployment requirement (must run via `dotnet watch run` in Codespaces)
- Public port forwarding must be accessible for audience devices
- SQLite single-file database (data loss on container restart is acceptable per spec assumptions)
- No production-grade auth (simple host token via environment variable)
- Single active poll at a time (creating new poll closes previous)
- Mobile-first responsive design (participants vote from phones)

**Scale/Scope**: 
- Conference room scale: 100 participants maximum per poll
- <10 total polls in dashboard (typical single-session usage)
- 2-6 options per poll
- Question text <500 chars, option text <200 chars

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Review against constitution principles (`.specify/memory/constitution.md`):

- [x] **Code Quality**: Linting/formatting rules defined (.editorconfig + StyleCop/Roslynator), .NET style guide, nullable reference types enabled
- [x] **Testing Standards**: TDD with xUnit, 80% coverage target (100% for vote counting logic), unit/integration/contract tests planned
- [x] **UX Consistency**: Responsive design patterns, WCAG 2.1 AA compliance required, actionable error messages, loading states for >500ms operations
- [x] **Performance**: Benchmarks from spec (PERF-001 to PERF-010), SignalR monitoring, SQL query indexing on poll codes, throttling for rapid votes
- [x] **Quality Gates**: CI/CD via GitHub Actions (linting, tests, coverage reports), performance regression detection planned

*No violations. All constitutional requirements addressed in Technical Context and will be validated in Phase 1 design.*

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
PulsePoll/                        # ASP.NET Core web application
├── PulsePoll.csproj             # Project file (.NET 10)
├── Program.cs                    # Application entry point, service configuration
├── appsettings.json             # Configuration (host token, DB connection)
├── appsettings.Development.json # Codespaces-specific config
│
├── Models/                       # Domain entities
│   ├── Poll.cs                  # Poll entity (Id, Code, Question, IsClosed, ChoiceMode)
│   ├── Option.cs                # Poll option (Id, PollId, Text)
│   ├── Vote.cs                  # Vote record (Id, PollId, OptionId, VoterId)
│   └── VoterIdentity.cs         # Voter tracking (generated GUID)
│
├── Data/                        # EF Core DbContext and migrations
│   ├── PollDbContext.cs        # EF Core context
│   └── Migrations/             # EF migrations
│
├── Services/                    # Business logic
│   ├── PollService.cs          # Poll CRUD, code generation, auto-close logic
│   ├── VoteService.cs          # Vote submission, duplicate prevention, updates
│   ├── QRCodeService.cs        # QR code generation
│   └── HostAuthService.cs      # Host token validation
│
├── Controllers/                 # MVC controllers for host dashboard
│   └── HostController.cs       # Create poll, view dashboard
│
├── Pages/                       # Razor Pages for participant views
│   ├── Vote.cshtml/.cs         # /p/{code} - voting page
│   ├── Results.cshtml/.cs      # /p/{code}/results - results display
│   └── Shared/                 # Layout, components
│       └── _Layout.cshtml
│
├── Hubs/                        # SignalR hubs
│   └── ResultsHub.cs           # Real-time vote update broadcasting
│
├── wwwroot/                     # Static files
│   ├── css/                    # Responsive styles, projection-optimized CSS
│   ├── js/                     # SignalR client, fallback polling
│   └── lib/                    # Client libraries (SignalR JS)
│
└── Middleware/                  # Custom middleware
    └── HostAuthMiddleware.cs   # Host token validation

tests/
├── PulsePoll.Tests.csproj      # Test project
├── Unit/                        # Unit tests
│   ├── PollServiceTests.cs     # Code generation, auto-close logic
│   ├── VoteServiceTests.cs     # Duplicate prevention, vote updates
│   └── QRCodeServiceTests.cs   # QR generation
├── Integration/                 # Integration tests
│   ├── PollCreationTests.cs    # End-to-end poll creation flow
│   ├── VotingFlowTests.cs      # Vote submission and retrieval
│   └── ResultsTests.cs         # Results aggregation
└── Contract/                    # API contract tests
    └── HostApiTests.cs         # Host endpoints, auth validation
```

**Structure Decision**: Single ASP.NET Core web application using MVC + Razor Pages hybrid. MVC controllers handle host dashboard (authenticated), Razor Pages handle participant flows (public). SignalR hub provides real-time updates. SQLite database file stored in app directory (ephemeral, acceptable per spec). Tests organized by type (unit/integration/contract) following constitutional TDD requirements.

## Complexity Tracking

*No constitutional violations requiring justification.*

**Acceptable Tradeoffs**:
- **Simple host auth (token-based)**: Justified for conference demo scope, documented in spec assumptions. Production-grade auth (OAuth, Identity) is out of scope.
- **SQLite ephemeral storage**: Acceptable per spec - data loss on Codespaces restart is expected. No backup/persistence beyond session lifetime required.
- **Single active poll constraint**: Simplifies concurrency, justified by conference use case (one poll at a time during presentation). Multi-poll support is out of scope.
- **SignalR with polling fallback**: Graceful degradation acceptable. Real-time updates are nice-to-have, periodic refresh meets functional requirements.

## Phase 0: Research (Complete)

All technology decisions documented in [research.md](research.md):
- ✅ ASP.NET Core MVC + Razor Pages hybrid architecture
- ✅ SignalR for real-time updates with JavaScript client fallback
- ✅ SQLite with EF Core, code-first migrations
- ✅ QRCoder library for QR generation
- ✅ Custom middleware for host token authentication
- ✅ HTTP cookie-based voter identity tracking
- ✅ Bootstrap 5 with mobile-first responsive design
- ✅ Global exception handler with user-friendly error pages
- ✅ Performance optimization: caching, indexing, throttling
- ✅ GitHub Codespaces configuration with .devcontainer

**Research Outcome**: Zero unknowns remaining. All technical approaches validated against spec requirements and constitutional principles.

## Phase 1: Design (Complete)

### Data Model
[data-model.md](data-model.md) defines:
- ✅ 3 core entities: Poll, Option, Vote
- ✅ Voter identity model (cookie-based, implicit)
- ✅ Relationships and foreign keys
- ✅ Validation rules (code length, option count, text limits)
- ✅ State transitions (Open → Closed)
- ✅ Indexes for performance (Poll.Code, Vote.VoterId, Vote.OptionId)
- ✅ Aggregated views (PollResults, VoterStatus)
- ✅ EF Core DbContext configuration

### API Contracts
[contracts/api-endpoints.md](contracts/api-endpoints.md) specifies:
- ✅ Host endpoints: POST /host/polls, GET /host/polls, GET /host/polls/{code}/qr
- ✅ Participant endpoints: GET /p/{code}, POST /p/{code}/vote, GET /p/{code}/results
- ✅ Results API: GET /api/results/{code}
- ✅ SignalR hub: ResultsHub with JoinPollGroup and VoteUpdated methods
- ✅ Request/response schemas with validation rules
- ✅ Error response format and HTTP status codes
- ✅ Authentication flow (host token, voter cookie)
- ✅ Performance SLA for each endpoint

### Quickstart Guide
[quickstart.md](quickstart.md) provides:
- ✅ Setup instructions for GitHub Codespaces
- ✅ Port forwarding configuration
- ✅ Quick test flow (create poll → vote → view results)
- ✅ Development workflow (tests, migrations, debugging)
- ✅ Troubleshooting common issues
- ✅ Architecture diagram

## Constitution Re-Check (Post-Design)

Validation against constitution principles:

### I. Code Quality Standards ✅
- **Linting/Formatting**: .editorconfig with .NET conventions, StyleCop analyzers planned
- **Documentation**: XML comments for public APIs, inline docs for complex logic (vote update transactions)
- **Naming**: Entities (Poll, Option, Vote), Services (PollService, VoteService), clear intent
- **Complexity**: Vote update logic wrapped in transaction (acceptable complexity, documented in data-model.md)
- **DRY**: QR generation cached per poll, results queries reusable

### II. Testing Standards ✅
- **TDD Workflow**: Tests written first per research.md (Red-Green-Refactor)
- **Coverage**: 80% minimum (100% for vote counting/duplicate prevention per spec)
- **Test Types**: Unit (services), Integration (end-to-end flows), Contract (API endpoints)
- **Isolation**: InMemory SQLite per integration test, Moq for service dependencies
- **Test Names**: Descriptive (CreatePoll_WithDuplicateOptions_ThrowsValidationException)

### III. UX Consistency ✅
- **Design Patterns**: Bootstrap 5 components, consistent navigation (host dashboard, participant flows)
- **Error Messages**: Actionable per contracts ("Poll not found. Check the code and try again")
- **Loading States**: Spinner for >500ms operations (poll creation, vote submission)
- **Accessibility**: WCAG 2.1 AA planned (keyboard nav, 4.5:1 contrast, screen reader labels)
- **Responsive**: Mobile-first CSS, breakpoints at 768px/1024px
- **Feedback**: Success ("Vote recorded!"), error states, validation messages

### IV. Performance Requirements ✅
- **API Latency**: Targets defined per endpoint (PERF-001 to PERF-010)
- **Indexing**: Poll.Code, Vote.VoterId, Vote.OptionId indexes in data model
- **Query Optimization**: LINQ with projections, no N+1 queries
- **Caching**: QR codes cached, results cached for 5s during rapid voting
- **SignalR Throttling**: Max 1 broadcast/second per research.md
- **Monitoring**: Planned via logging (Serilog) and performance metrics

### Quality Gates (CI/CD) ✅
- **Linting**: dotnet format, StyleCop analyzers
- **Tests**: dotnet test with 80% coverage enforcement
- **Coverage Reports**: Coverlet or dotCover
- **Performance**: Load tests with k6 (planned in quickstart.md)
- **Accessibility**: Manual audit checklist (WCAG 2.1 AA)
- **Security**: No critical vulnerabilities (NuGet audit)

**Final Verdict**: All constitutional principles satisfied. Design ready for implementation.

## Next Steps

1. **Generate Tasks**: Run `/speckit.tasks` to break down user stories into actionable tasks
2. **Setup Project**: Follow [quickstart.md](quickstart.md) to create .NET project in Codespaces
3. **Phase 2 Implementation**: Begin with P1 user story (Create and Join Poll) following TDD workflow
4. **Iterate**: P2 (Live Results) → P3 (Vote Updates) → P4 (Multi-Choice) → P5 (Close Poll) → P6 (QR Codes)

**Ready for**: `/speckit.tasks` command to generate task breakdown (tasks.md)

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
