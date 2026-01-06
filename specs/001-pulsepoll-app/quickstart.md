# Quickstart: PulsePoll Development

**Feature**: PulsePoll - Conference Polling App  
**Date**: 2026-01-06  
**Target**: Developers setting up the project in GitHub Codespaces

## Prerequisites

- GitHub account with Codespaces access
- Basic familiarity with .NET and C#
- Modern web browser (Chrome, Firefox, Safari, Edge)

## Setup (5 minutes)

### 1. Open in Codespaces

```bash
# From GitHub repository page, click "Code" → "Codespaces" → "Create codespace on main"
# Or use GitHub CLI:
gh codespace create --repo Ba4bes/Pollpoll --branch 001-pulsepoll-app
```

**What happens**:
- Codespaces provisions a Linux container
- `.devcontainer/devcontainer.json` installs .NET 10 SDK
- Workspace opens in VS Code (web or desktop)

### 2. Verify .NET Installation

```bash
dotnet --version
# Expected output: 10.0.x
```

### 3. Restore Dependencies

```bash
cd PulsePoll
dotnet restore
```

**Packages installed**:
- ASP.NET Core 10.0
- Entity Framework Core 10.0 (SQLite provider)
- SignalR Core
- QRCoder
- xUnit (test framework)

### 4. Apply Database Migrations

```bash
dotnet ef database update
```

**Result**: Creates `pollpoll.db` SQLite file with schema (Polls, Options, Votes tables)

### 5. Configure Host Token

**Option A**: Edit `appsettings.Development.json`
```json
{
  "HostToken": "demo2026"
}
```

**Option B**: Set environment variable
```bash
export HOST_TOKEN="demo2026"
```

### 6. Run the Application

```bash
dotnet run
# Or for hot reload during development:
dotnet watch run
```

**Expected output**:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### 7. Configure Port Forwarding

1. **VS Code**: Ports tab (bottom panel) → Port 5000 should auto-forward
2. **Set to Public**: Right-click port 5000 → "Port Visibility" → "Public"
3. **Copy URL**: Right-click → "Copy Local Address"
   - Example: `https://super-disco-abc123-5000.app.github.dev`

### 8. Test the Application

**Open in Browser**: Paste the copied URL

**Expected landing page**:
- Host dashboard at `/host` (requires token authentication)
- Or create first poll to generate join URL

## Quick Test Flow (2 minutes)

### Create a Poll

1. Navigate to `/host` (or `/host/login` if cookie not set)
2. Enter host token: `demo2026`
3. Click "Create New Poll"
4. Fill in:
   - Question: "What's your favorite programming language?"
   - Choice mode: Single
   - Options: C#, Python, JavaScript
5. Submit → Receive poll code (e.g., "A7X9") and QR code

### Vote as Participant

1. Open new incognito/private window (simulates different participant)
2. Navigate to `https://<codespaces-url>/p/A7X9`
3. Select an option (e.g., "C#")
4. Submit vote → See confirmation message

### View Live Results

1. Original window: Navigate to `https://<codespaces-url>/p/A7X9/results`
2. Keep page open
3. In incognito window: Change vote to "Python"
4. **Expected**: Results page updates automatically within 2 seconds (SignalR)
5. See vote counts update: C# (0), Python (1), JavaScript (0)

### Test on Mobile

1. Display QR code from poll creation page
2. Scan with phone camera
3. Vote from mobile browser
4. Verify responsive layout and touch-friendly buttons

## Development Workflow

### Run Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter Category=Unit

# Integration tests only
dotnet test --filter Category=Integration

# With coverage report
dotnet test /p:CollectCoverage=true /p:CoverageReporter=html
# Report: tests/TestResults/coverage.html
```

**Expected**: 80% minimum coverage (100% for vote counting logic)

### Database Migrations

**Create migration**:
```bash
dotnet ef migrations add <MigrationName>
# Example: dotnet ef migrations add AddQRCodeCaching
```

**Apply migration**:
```bash
dotnet ef database update
```

**Rollback**:
```bash
dotnet ef database update <PreviousMigrationName>
```

### Debugging

**VS Code**:
1. Set breakpoints in code
2. F5 (Run → Start Debugging)
3. Select ".NET Core Launch (web)" configuration

**Browser DevTools**:
- Check SignalR connection: Console → Look for "SignalR connected"
- Network tab: Monitor API calls (`/api/results/{code}`)

### Hot Reload

```bash
dotnet watch run
```

**Behavior**: Code changes trigger automatic rebuild and browser refresh (no manual restart)

## Common Tasks

### Add New Poll Option Validation

1. **Write test first** (`tests/Unit/PollServiceTests.cs`):
   ```csharp
   [Fact]
   public void CreatePoll_WithDuplicateOptions_ThrowsValidationException()
   {
       // Arrange, Act, Assert
   }
   ```

2. **Run test**: `dotnet test --filter CreatePoll_WithDuplicateOptions` → Should FAIL (Red)

3. **Implement** in `Services/PollService.cs`:
   ```csharp
   if (options.Select(o => o.Text).Distinct().Count() != options.Count)
       throw new ValidationException("Duplicate option text");
   ```

4. **Run test again** → Should PASS (Green)

5. **Refactor** if needed

### Update SignalR Hub Logic

1. Modify `Hubs/ResultsHub.cs`
2. Test manually: Open results page, submit vote, verify broadcast
3. Add integration test in `tests/Integration/SignalRTests.cs`

### Change Database Schema

1. Modify entity in `Models/Poll.cs`
2. Create migration: `dotnet ef migrations add <Name>`
3. Review migration file in `Data/Migrations/`
4. Apply: `dotnet ef database update`
5. Update tests to reflect schema changes

## Configuration

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.SignalR": "Debug"
    }
  },
  "HostToken": "demo2026",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=pollpoll.db"
  },
  "Codespaces": {
    "PublicUrl": "https://super-disco-abc123-5000.app.github.dev"
  }
}
```

### Environment Variables (Overrides)

```bash
export HOST_TOKEN="production-token-here"
export ASPNETCORE_ENVIRONMENT="Development"
```

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│              Browser (Participant)              │
│  ┌──────────┐  ┌──────────┐  ┌──────────────┐  │
│  │  Voting  │  │ Results  │  │ SignalR JS   │  │
│  │   Form   │  │  Display │  │   Client     │  │
│  └─────┬────┘  └────┬─────┘  └──────┬───────┘  │
└────────┼────────────┼────────────────┼──────────┘
         │            │                │
    POST /p/{code}   GET /api/results  WS /hubs/results
         │            │                │
┌────────▼────────────▼────────────────▼──────────┐
│           ASP.NET Core Web App                  │
│  ┌──────────────┐  ┌─────────────────────────┐ │
│  │ Razor Pages  │  │   MVC Controllers       │ │
│  │ (Participant)│  │   (Host Dashboard)      │ │
│  └──────┬───────┘  └──────┬──────────────────┘ │
│         │                 │                     │
│  ┌──────▼─────────────────▼──────────────────┐ │
│  │          Services Layer                   │ │
│  │  ┌──────────┐ ┌──────────┐ ┌───────────┐ │ │
│  │  │PollSvc   │ │VoteSvc   │ │QRCodeSvc  │ │ │
│  │  └────┬─────┘ └────┬─────┘ └───────────┘ │ │
│  └───────┼────────────┼───────────────────────┘ │
│          │            │                         │
│  ┌───────▼────────────▼──────────┐              │
│  │   EF Core DbContext           │              │
│  │   (SQLite Provider)           │              │
│  └───────────────┬───────────────┘              │
│                  │                               │
│  ┌───────────────▼───────────────┐              │
│  │      pollpoll.db (SQLite)     │              │
│  │   ┌──────┐ ┌────────┐ ┌─────┐ │              │
│  │   │Polls │ │Options │ │Votes│ │              │
│  │   └──────┘ └────────┘ └─────┘ │              │
│  └───────────────────────────────┘              │
│                                                  │
│  ┌─────────────────────────────────────────┐    │
│  │       SignalR Hub (ResultsHub)          │    │
│  │  Broadcasts vote updates to all clients │    │
│  └─────────────────────────────────────────┘    │
└──────────────────────────────────────────────────┘
```

## Performance Targets

Validate these during development:

| Operation | Target | How to Measure |
|-----------|--------|----------------|
| Poll creation | <500ms | Browser DevTools Network tab |
| Vote submission | <300ms (p95) | Load test with k6 or Apache Bench |
| Results page load | <2s (3G) | Chrome DevTools → Network throttling → Slow 3G |
| SignalR update | <2s latency | Manual: Submit vote, time until results update |

**Load Testing Example**:
```bash
# Install k6: https://k6.io/docs/getting-started/installation
k6 run tests/load/vote-submission.js
```

## Troubleshooting

### Port 5000 Not Forwarding

**Symptoms**: Can't access app from browser, connection refused

**Fix**:
1. Check Ports tab: Port 5000 should be listed
2. If not: Manually add forward (`Ports` → `+` → Enter `5000`)
3. Set visibility to Public
4. Restart app: `dotnet run`

### Database Locked Error

**Symptoms**: `SQLite Error 5: 'database is locked'`

**Fix**:
- SQLite single-writer limitation
- Ensure no other process has `pollpoll.db` open
- Restart app
- In production: Consider connection pooling config

### SignalR Not Connecting

**Symptoms**: Results page doesn't update automatically, console shows connection errors

**Fix**:
1. Check console for errors: `SignalR connection failed`
2. Verify WebSocket support in browser
3. Check CORS configuration (shouldn't be needed in Codespaces)
4. Fallback: Periodic polling should activate automatically (5s intervals)

### QR Code Not Displaying

**Symptoms**: Blank image or broken image icon

**Fix**:
1. Verify QRCoder NuGet package installed: `dotnet list package`
2. Check `QRCodeService.cs` implementation
3. Inspect response: QR code should be Base64 data URL

### Host Token Not Working

**Symptoms**: 401 Unauthorized on `/host/*` endpoints

**Fix**:
1. Verify token in `appsettings.Development.json` matches input
2. Check environment variable: `echo $HOST_TOKEN`
3. Clear cookies and re-login
4. Check middleware order in `Program.cs`: `UseAuthentication()` before `UseAuthorization()`

## Next Steps

After successful setup:

1. **Read data model**: `specs/001-pulsepoll-app/data-model.md`
2. **Review API contracts**: `specs/001-pulsepoll-app/contracts/api-endpoints.md`
3. **Run task breakdown**: `/speckit.tasks` to generate implementation tasks
4. **Start TDD workflow**: Write tests for User Story 1 (P1 priority)

## Resources

- **.NET 10 Docs**: https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10
- **ASP.NET Core**: https://learn.microsoft.com/aspnet/core/
- **SignalR**: https://learn.microsoft.com/aspnet/core/signalr/
- **EF Core**: https://learn.microsoft.com/ef/core/
- **Codespaces**: https://docs.github.com/codespaces

## Support

For questions or issues:
1. Check this quickstart guide
2. Review specification: `specs/001-pulsepoll-app/spec.md`
3. Check implementation plan: `specs/001-pulsepoll-app/plan.md`
4. Open issue in repository
