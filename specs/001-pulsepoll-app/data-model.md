# Data Model: PulsePoll

**Feature**: PulsePoll - Conference Polling App  
**Phase**: 1 - Design  
**Date**: 2026-01-06

## Purpose

Define the domain entities, relationships, validation rules, and state transitions for the PulsePoll conference polling application.

## Core Entities

### Poll

Represents a conference poll with a unique join code, question, and options.

**Properties**:
- `Id` (int, PK): Auto-increment primary key
- `Code` (string, unique, indexed): 4-character alphanumeric join code (e.g., "K7F3")
- `Question` (string, max 500 chars): Poll question text
- `ChoiceMode` (enum: Single, Multi): Whether participants can select one or multiple options
- `IsClosed` (bool, default false): Poll status (open for voting vs closed)
- `CreatedAt` (DateTime): Poll creation timestamp
- `ClosedAt` (DateTime?, nullable): When poll was closed (null if still open)

**Validation Rules**:
- `Code`: Exactly 4 characters, alphanumeric (A-Z, 0-9), case-insensitive stored as uppercase
- `Question`: Required, 1-500 characters
- `ChoiceMode`: Required, must be Single or Multi
- Must have 2-6 Options (validated at service layer, not entity level)

**Relationships**:
- Has many `Option` (cascade delete)
- Has many `Vote` (cascade delete)

**Indexes**:
- Unique index on `Code` (primary lookup pattern)
- Index on `IsClosed` (for dashboard filtering)

**State Transitions**:
```
[New Poll Created] → IsClosed = false
                   ↓
              [Voting Open]
                   ↓
         [New Poll Created or Manual Close]
                   ↓
              IsClosed = true, ClosedAt = DateTime.UtcNow
                   ↓
           [Voting Disabled]
```

**Invariants**:
- A poll can transition from Open → Closed but never Closed → Open
- Only one poll should be open at a time (enforced by PollService auto-close logic)
- ClosedAt must be null when IsClosed = false, and not null when IsClosed = true

---

### Option

Represents a single answer choice within a poll.

**Properties**:
- `Id` (int, PK): Auto-increment primary key
- `PollId` (int, FK): Foreign key to Poll
- `Text` (string, max 200 chars): Option text (e.g., "Red", "Blue", "Green")
- `DisplayOrder` (int): Order for display (0-based)

**Validation Rules**:
- `Text`: Required, 1-200 characters
- `DisplayOrder`: Required, 0-based sequential (0, 1, 2, 3, 4, 5)

**Relationships**:
- Belongs to one `Poll` (required)
- Has many `Vote` (cascade delete)

**Indexes**:
- Index on `PollId` (querying options for a poll)

**Invariants**:
- DisplayOrder must be unique within a poll
- Cannot delete option if votes exist (enforced by cascade delete - deleting poll deletes votes first)

---

### Vote

Represents a participant's vote selection.

**Properties**:
- `Id` (int, PK): Auto-increment primary key
- `PollId` (int, FK): Foreign key to Poll
- `OptionId` (int, FK): Foreign key to Option
- `VoterId` (Guid): Unique voter identifier from cookie
- `SubmittedAt` (DateTime): When vote was cast

**Validation Rules**:
- `VoterId`: Required, valid GUID
- `OptionId`: Must belong to the same poll as `PollId` (validated at service layer)

**Relationships**:
- Belongs to one `Poll` (required)
- Belongs to one `Option` (required)

**Indexes**:
- Composite index on `(PollId, VoterId)` for duplicate detection
- Index on `OptionId` for aggregation queries

**Constraints**:
- **Single-choice polls**: One vote per VoterId per Poll (enforced by deleting old vote before inserting new)
- **Multi-choice polls**: Multiple votes per VoterId per Poll allowed, but max one vote per Option per VoterId (prevent selecting same option twice)

**State Transitions**:
```
[First Vote]
   VoterId not in Votes for PollId
   → INSERT Vote

[Update Vote - Single Choice]
   VoterId exists in Votes for PollId
   → DELETE old Vote WHERE PollId AND VoterId
   → INSERT new Vote

[Update Vote - Multi Choice]
   User selects new set of options
   → DELETE all Votes WHERE PollId AND VoterId
   → INSERT new Votes for each selected OptionId
```

---

### VoterIdentity (Implicit/Cookie-Based)

Not a database entity but a logical concept represented by HTTP cookie.

**Properties**:
- `VoterId` (Guid): Stored in cookie `PulsePollVoterId`
- `Expiration`: 7 days from first vote

**Behavior**:
- Generated on first vote submission if cookie doesn't exist
- Used to identify repeat voters for duplicate prevention
- Cookie loss results in new VoterId (acceptable - treated as new voter)

---

## Aggregated Views (Non-Entities)

These are computed at query time, not stored.

### PollResults

Aggregated vote counts for display on results page.

**Structure**:
```csharp
public class PollResults
{
    public string PollCode { get; set; }
    public string Question { get; set; }
    public bool IsClosed { get; set; }
    public int TotalVotes { get; set; }
    public List<OptionResult> Options { get; set; }
}

public class OptionResult
{
    public int OptionId { get; set; }
    public string Text { get; set; }
    public int VoteCount { get; set; }
    public decimal Percentage { get; set; }  // VoteCount / TotalVotes * 100
}
```

**Query**:
```csharp
var results = await _context.Polls
    .Where(p => p.Code == code)
    .Select(p => new PollResults
    {
        PollCode = p.Code,
        Question = p.Question,
        IsClosed = p.IsClosed,
        TotalVotes = p.Votes.Count(),
        Options = p.Options.Select(o => new OptionResult
        {
            OptionId = o.Id,
            Text = o.Text,
            VoteCount = o.Votes.Count(),
            Percentage = p.Votes.Count() > 0 
                ? (decimal)o.Votes.Count() / p.Votes.Count() * 100 
                : 0
        }).ToList()
    })
    .FirstOrDefaultAsync();
```

---

### VoterStatus

Indicates whether a voter has already voted and what their selections were.

**Structure**:
```csharp
public class VoterStatus
{
    public bool HasVoted { get; set; }
    public List<int> SelectedOptionIds { get; set; }
}
```

**Query**:
```csharp
var status = await _context.Votes
    .Where(v => v.PollId == pollId && v.VoterId == voterId)
    .Select(v => v.OptionId)
    .ToListAsync();

return new VoterStatus
{
    HasVoted = status.Any(),
    SelectedOptionIds = status
};
```

---

## Validation Rules Summary

| Field | Rule |
|-------|------|
| Poll.Code | Exactly 4 chars, alphanumeric, unique |
| Poll.Question | 1-500 characters |
| Poll Options Count | 2-6 options per poll |
| Option.Text | 1-200 characters |
| Vote.VoterId | Valid GUID from cookie |
| Single-choice vote | Max 1 vote per VoterId per Poll |
| Multi-choice vote | Max 1 vote per VoterId per Option |

---

## Database Schema (EF Core)

```csharp
public class PollDbContext : DbContext
{
    public DbSet<Poll> Polls { get; set; }
    public DbSet<Option> Options { get; set; }
    public DbSet<Vote> Votes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Poll configuration
        modelBuilder.Entity<Poll>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(4);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.HasIndex(p => p.IsClosed);
            entity.Property(p => p.Question).IsRequired().HasMaxLength(500);
        });

        // Option configuration
        modelBuilder.Entity<Option>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Text).IsRequired().HasMaxLength(200);
            entity.HasIndex(o => o.PollId);
            
            entity.HasOne<Poll>()
                .WithMany()
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Vote configuration
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => new { v.PollId, v.VoterId });
            entity.HasIndex(v => v.OptionId);
            
            entity.HasOne<Poll>()
                .WithMany()
                .HasForeignKey(v => v.PollId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne<Option>()
                .WithMany()
                .HasForeignKey(v => v.OptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

---

## Data Flow Examples

### Create Poll Flow
```
Input: { Question: "Favorite color?", ChoiceMode: Single, Options: ["Red", "Blue"] }
  ↓
1. Generate unique 4-char code: "K7F3"
2. Auto-close previous open poll: UPDATE Polls SET IsClosed=true WHERE IsClosed=false
3. Create Poll entity: { Code: "K7F3", Question: "...", IsClosed: false, CreatedAt: now }
4. Create Option entities: [{ PollId: 1, Text: "Red", Order: 0 }, { Text: "Blue", Order: 1 }]
  ↓
Output: { PollCode: "K7F3", QRCodeUrl: "data:image/png;base64,..." }
```

### Vote Submission Flow (Single-Choice)
```
Input: { PollCode: "K7F3", SelectedOptionId: 2, VoterId: <GUID from cookie> }
  ↓
1. Validate poll exists and is open
2. Validate option belongs to poll
3. Check for existing vote: SELECT * FROM Votes WHERE PollId=1 AND VoterId=<GUID>
4a. If exists: DELETE FROM Votes WHERE Id=<existingVoteId>
4b. Then: INSERT INTO Votes (PollId, OptionId, VoterId, SubmittedAt) VALUES (...)
5. Broadcast update via SignalR: ResultsHub.SendAsync("VoteUpdated", pollCode)
  ↓
Output: { Success: true, Message: "Vote recorded!" }
```

### Results Aggregation Flow
```
Input: { PollCode: "K7F3" }
  ↓
1. Query poll with options and vote counts
2. Calculate percentages
3. Order by DisplayOrder
  ↓
Output: {
  Question: "Favorite color?",
  IsClosed: false,
  TotalVotes: 5,
  Options: [
    { Text: "Red", VoteCount: 3, Percentage: 60 },
    { Text: "Blue", VoteCount: 2, Percentage: 40 }
  ]
}
```

---

## Migration Strategy

**Initial Migration**:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**Seed Data** (optional for development):
- Sample poll with code "TEST" for manual testing
- Not required for production (polls created via UI)

---

## Performance Considerations

- **Code lookup**: Indexed on `Poll.Code` (most frequent query)
- **Vote aggregation**: Indexed on `Vote.OptionId` for GROUP BY queries
- **Duplicate detection**: Composite index on `(PollId, VoterId)` for fast lookups
- **Results caching**: Cache poll results in memory for 5 seconds to reduce DB load during rapid voting

---

## Data Integrity

- **Referential integrity**: Foreign keys with cascade delete ensure no orphaned votes/options
- **Concurrency**: SQLite single-writer limitation acceptable for conference scale (100 participants)
- **Transaction boundaries**: Vote updates wrapped in transaction (delete old + insert new as atomic operation)
