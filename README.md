# PollPoll - Conference Polling App

**Real-time audience polling for conferences and presentations**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Overview

PollPoll is a lightweight, real-time polling application designed for conference speakers and presenters. Create polls instantly, share a join code or QR code with your audience, and display live results on screen as votes come in.

**Key Features:**
- ✨ **Instant Poll Creation** - Create polls in seconds with 2-6 multiple choice options
- 📱 **Mobile-First Voting** - Participants vote from their phones via simple join codes
- 📊 **Live Results** - Real-time updates via SignalR with automatic fallback polling
- 🔒 **Duplicate Prevention** - Cookie-based voter tracking prevents multiple votes
- ✅ **Vote Updates** - Participants can change their vote at any time
- 🎯 **Single & Multi-Choice** - Support for both single-select and multi-select polls
- 📦 **Zero Deployment** - Runs in GitHub Codespaces with public port forwarding
- ♿ **Accessible** - WCAG 2.1 AA compliant with keyboard navigation and screen reader support

## Quick Start

**For detailed setup instructions, see [specs/001-pulsepoll-app/quickstart.md](specs/001-pulsepoll-app/quickstart.md)**

### 1. Open in GitHub Codespaces

```bash
# Click "Code" → "Codespaces" → "Create codespace" on GitHub
# Or use GitHub CLI:
gh codespace create --repo Ba4bes/Pollpoll --branch 001-pulsepoll-app
```

### 2. Run the Application

```bash
cd PollPoll
dotnet restore
dotnet ef database update
dotnet run
```

### 3. Configure Port Forwarding

1. Open **Ports** tab in VS Code (bottom panel)
2. Right-click port **5000** → **Port Visibility** → **Public**
3. Copy the forwarded URL (e.g., `https://your-codespace-5000.app.github.dev`)

### 4. Create Your First Poll

1. Navigate to `/host` in your browser
2. Enter host token (default: check `appsettings.Development.json`)
3. Create poll with question and 2-6 options
4. Share join code or QR code with audience
5. Display results at `/p/{code}/results`

## Architecture

- **Framework**: .NET 10 (C# 13), ASP.NET Core 10.0
- **Database**: SQLite with Entity Framework Core 10
- **Real-time**: SignalR Core for live result updates
- **Testing**: xUnit with FluentAssertions (80%+ coverage target)
- **UI**: Razor Pages with Bootstrap 5, mobile-responsive design

## Development

### Prerequisites

- .NET 10 SDK
- GitHub account with Codespaces access (or local Docker)
- Modern web browser

### Run Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter "FullyQualifiedName~PollPoll.Tests.Unit"

# Integration tests only
dotnet test --filter "FullyQualifiedName~PollPoll.Tests.Integration"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverageReporter=html
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add <MigrationName>

# Apply migrations
dotnet ef database update

# Rollback
dotnet ef database update <PreviousMigrationName>
```

### Hot Reload Development

```bash
dotnet watch run
```

## Project Structure

```
PollPoll/                     # Main web application
├── Controllers/             # Host dashboard controllers
├── Pages/                   # Razor pages for voting/results
├── Models/                  # Domain entities (Poll, Option, Vote)
├── Services/                # Business logic layer
├── Data/                    # EF Core DbContext and migrations
├── Hubs/                    # SignalR hubs for real-time updates
├── Middleware/              # Custom middleware (auth, errors)
└── wwwroot/                 # Static assets (CSS, JS)

PollPoll.Tests/              # Test project
├── Unit/                    # Service layer unit tests
├── Integration/             # End-to-end integration tests
└── Contract/                # API contract tests

specs/001-pulsepoll-app/     # Feature specifications
├── spec.md                  # User stories and requirements
├── plan.md                  # Technical implementation plan
├── data-model.md            # Database schema
├── quickstart.md            # Developer setup guide
├── tasks.md                 # Task breakdown
└── contracts/               # API specifications
```

## Usage Example

### As a Conference Speaker (Host)

1. **Before your talk**: Create poll with question like "What's your experience with .NET?"
2. **Display QR code**: Show on slide or handout printed code
3. **Audience votes**: Participants scan QR or visit `/p/{CODE}` and select their answer
4. **Live discussion**: Project `/p/{CODE}/results` and discuss as votes come in
5. **Multiple polls**: Create new polls throughout presentation (supports multiple active polls)
6. **Close poll**: Manually close poll when done or let it auto-expire after 7 days

### As an Audience Member (Participant)

1. **Join poll**: Scan QR code or enter join code at `/p/{CODE}`
2. **Vote**: Select your answer from options
3. **Update vote**: Change your mind? Submit again to update (previous vote replaced)
4. **See results**: Navigate to results page to see how others voted

## Performance

Meets the following benchmarks (from specification):

- **Poll creation**: <500ms
- **Vote submission**: <300ms (p95)
- **Results page load**: <2s on 3G
- **SignalR updates**: <2s latency
- **Concurrent voters**: 100+ supported

## Testing

**Test-Driven Development (TDD)** workflow:
1. ✅ **Red**: Write failing test
2. ✅ **Green**: Implement minimum code to pass
3. ✅ **Refactor**: Improve code quality

**Coverage targets**:
- Unit tests: 80%+ overall, 100% for vote counting logic
- Integration tests: End-to-end user scenarios
- Contract tests: API request/response validation

## Accessibility

WCAG 2.1 AA compliant:
- ✅ Keyboard navigation for all interactive elements
- ✅ ARIA labels and live regions for screen readers
- ✅ 4.5:1 minimum color contrast ratio
- ✅ Focus indicators with 2px outline
- ✅ Semantic HTML with proper heading hierarchy
- ✅ Form field labels and error messages

## Browser Support

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

## Security

- **Host Authentication**: Simple token-based auth (not production-grade)
- **Voter Tracking**: Browser cookies (GUID-based, no PII)
- **Input Validation**: Question/option length limits, option count validation
- **SQL Injection**: Protected via EF Core parameterized queries

**⚠️ Note**: This is a conference demo app. For production use, implement proper authentication, HTTPS, rate limiting, and input sanitization.

## Documentation

- **[Quickstart Guide](specs/001-pulsepoll-app/quickstart.md)** - Complete setup and development workflow
- **[Specification](specs/001-pulsepoll-app/spec.md)** - User stories, requirements, success criteria
- **[Implementation Plan](specs/001-pulsepoll-app/plan.md)** - Technical architecture and design decisions
- **[Data Model](specs/001-pulsepoll-app/data-model.md)** - Database schema and entity relationships
- **[API Contracts](specs/001-pulsepoll-app/contracts/)** - Endpoint specifications and test requirements
- **[Task Breakdown](specs/001-pulsepoll-app/tasks.md)** - Implementation task list

## Troubleshooting

See [quickstart.md](specs/001-pulsepoll-app/quickstart.md#troubleshooting) for common issues:
- Port forwarding not working
- Database locked errors
- SignalR connection failures
- QR code display issues

## Contributing

This is a demo project built following the SpecKit workflow. For contributing guidelines:

1. Review [spec.md](specs/001-pulsepoll-app/spec.md) for requirements
2. Follow TDD workflow (tests first)
3. Ensure 80%+ test coverage
4. Run `dotnet test` before submitting PR
5. Follow .NET coding conventions

## License

MIT License - see LICENSE file for details

## Acknowledgments

Built with:
- [ASP.NET Core](https://asp.net)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [SignalR](https://dotnet.microsoft.com/apps/aspnet/signalr)
- [QRCoder](https://github.com/codebude/QRCoder)
- [Bootstrap 5](https://getbootstrap.com)

---

**Made for conference speakers who want instant audience feedback** 🎤📊