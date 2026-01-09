# Plan: Multiple Active Polls with Separate Archive Page

Add archive system with separate Archive page in navbar. Active polls sorted by status (open first) then date descending, showing full details. Archive page enables poll retirement and permanent deletion with confirmation.

## Steps

1. **Add `IsArchived` property** to `Poll` model ([Poll.cs](PollPoll/Models/Poll.cs#L45)) with default `false`, create migration adding indexed `IsArchived` column to Polls table.

2. **Add service methods** to `PollService` ([PollService.cs](PollPoll/Services/PollService.cs#L176-L188)): `ArchivePollAsync(code)` sets `IsArchived = true`, `UnarchivePollAsync(code)` sets `IsArchived = false`, `DeletePollAsync(code)` removes poll with cascade delete, update `GetAllPollsAsync()` to accept optional `isArchived` filter parameter.

3. **Update Index page** ([Index.cshtml.cs](PollPoll/Pages/Index.cshtml.cs), [Index.cshtml](PollPoll/Pages/Index.cshtml)) to load non-archived polls ordered by `IsClosed ASC, CreatedAt DESC`, display full-width cards with all options, created date, closed date (if closed), vote count, status badge, QR code, action buttons (Open Voting, View Results in new tab, Close for open polls, Archive for closed polls), add `OnPostArchivePollAsync` handler using TempData for success messages.

4. **Create Archive page** (new `PollPoll/Pages/Archive.cshtml` and `Archive.cshtml.cs`) with `[HostAuthorization]` attribute, loading archived polls, full-width cards showing all details including dates/vote counts, results link opening in new tab, Unarchive and Delete buttons with `onclick="return confirm('Are you sure...')"`, handlers `OnPostUnarchivePollAsync` and `OnPostDeletePollAsync` using TempData.

5. **Update navbar** in [_Layout.cshtml](PollPoll/Pages/Shared/_Layout.cshtml#L23-L28) to add Archive navigation link after Home link.

6. **Update QR code script** in [Index.cshtml](PollPoll/Pages/Index.cshtml#L233-L270) to use `querySelectorAll` for multiple `.qr-code-container` elements, loading each poll's QR code via `/host/polls/{code}/qr` endpoint.
