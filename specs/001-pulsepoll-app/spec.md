# Feature Specification: PulsePoll - Conference Polling App

**Feature Branch**: `001-pulsepoll-app`  
**Created**: 2026-01-06  
**Status**: Draft  
**Input**: User description: "PulsePoll (Codespaces-hosted conference polling) - A small web app that lets a speaker create a poll, share a join link/QR with the room, collect votes from phones, and show live-updating results on a projector"

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Create and Join Poll (Priority: P1)

A conference host creates a simple poll with a question and answer options, receives a join code and URL, and an audience member successfully joins and votes using their phone.

**Why this priority**: This is the core value proposition - the absolute minimum for a functional polling system. Without this, there is no product.

**Independent Test**: Can be fully tested by creating a poll through the host interface, receiving a join code/URL, navigating to that URL from a different device/browser, selecting an option, and submitting the vote. Delivers immediate value by enabling basic polling.

**Acceptance Scenarios**:

1. **Given** I am a host with valid host token, **When** I create a poll with question "What's your favorite color?" and options ["Red", "Blue", "Green"], **Then** I receive a 4-character join code (e.g., "K7F3") and join URL `/p/K7F3`
2. **Given** a poll with code "K7F3" exists, **When** a participant navigates to `/p/K7F3`, **Then** they see the question and all options with a submit button
3. **Given** a participant is viewing poll "K7F3", **When** they select "Blue" and click submit, **Then** their vote is recorded and they see confirmation
4. **Given** a participant has already voted on poll "K7F3", **When** they return to `/p/K7F3`, **Then** they see their previous selection highlighted
5. **Given** poll creation supports single-choice mode, **When** participant selects one option, **Then** only that option can be selected (radio button behavior)

---

### User Story 2 - Live Results Display (Priority: P2)

After votes are submitted, the host can view live-updating results on a projector-friendly display that shows vote counts and percentages as they come in.

**Why this priority**: Real-time feedback is what makes this compelling for conference use. Static results could work but live updates create engagement and energy in the room.

**Independent Test**: Can be tested by opening results page `/p/{code}/results` on a projector display while participants vote from phones. Results update automatically without page refresh, showing vote distribution as a histogram or table.

**Acceptance Scenarios**:

1. **Given** poll "K7F3" has received 3 votes (2 for "Blue", 1 for "Red"), **When** host navigates to `/p/K7F3/results`, **Then** they see vote counts and percentages for each option (Blue: 2 votes/67%, Red: 1 vote/33%, Green: 0 votes/0%)
2. **Given** results page is open on projector, **When** a new participant submits a vote, **Then** the results update automatically within 2 seconds without page refresh (SignalR real-time)
3. **Given** real-time connection fails or is unavailable, **When** results page is open, **Then** it falls back to polling/periodic refresh every 5 seconds
4. **Given** results page is displaying data, **When** votes come in, **Then** display uses large, readable fonts suitable for projection (minimum 24px for main content)
5. **Given** total votes exceed zero, **When** viewing results, **Then** total vote count is prominently displayed (e.g., "Total votes: 15")

---

### User Story 3 - Vote Update and Duplicate Prevention (Priority: P3)

Participants can change their vote, and the system prevents duplicate votes from the same user by tracking voter identity.

**Why this priority**: Prevents ballot stuffing and allows participants to change their mind, improving data quality and user experience. Less critical than core voting functionality.

**Independent Test**: Can be tested by voting from the same browser/device twice - second vote should update the first rather than creating a duplicate. Voter identity is tracked via session/cookie.

**Acceptance Scenarios**:

1. **Given** participant has voted "Blue" on poll "K7F3", **When** they select "Red" and submit again, **Then** their original "Blue" vote is removed and "Red" vote is recorded (vote count remains correct)
2. **Given** participant is voting for the first time, **When** they submit a vote, **Then** system generates and stores a unique voter identifier (cookie or session-based)
3. **Given** participant has voter ID stored, **When** they return to poll page, **Then** their previous vote is highlighted/pre-selected
4. **Given** participant changes their vote 3 times, **When** viewing results, **Then** only their most recent vote counts (no duplicate counting)

---

### User Story 4 - Multi-Choice Polls (Priority: P4)

Host can create polls that allow participants to select multiple options (e.g., "Select all that apply").

**Why this priority**: Enhances flexibility and enables different types of questions, but single-choice covers most conference polling needs.

**Independent Test**: Can be tested by creating a poll with multi-choice mode enabled, then selecting 2+ options and submitting. Results show each option's vote count independently.

**Acceptance Scenarios**:

1. **Given** host creates poll with multi-choice mode enabled, **When** participant views the poll, **Then** they see checkboxes instead of radio buttons
2. **Given** participant is viewing multi-choice poll, **When** they select "Red", "Blue", and "Green" (3 options), **Then** all three selections are highlighted
3. **Given** participant submits multi-choice vote with 3 selections, **When** viewing results, **Then** each selected option's count increases by 1 (total vote count may exceed participant count)
4. **Given** participant updates multi-choice vote from ["Red", "Blue"] to ["Green", "Yellow"], **When** results update, **Then** Red and Blue counts decrease by 1, Green and Yellow counts increase by 1

---

### User Story 5 - Close Poll and Results Persistence (Priority: P5)

Host can close a poll to stop accepting new votes, and results remain viewable after closure. Creating a new poll automatically closes the previous poll.

**Why this priority**: Important for managing poll lifecycle and preserving historical data, but not critical for initial demo functionality.

**Independent Test**: Can be tested by closing an active poll through host dashboard, then attempting to vote (should be blocked). Results page should display "CLOSED" status and remain accessible.

**Acceptance Scenarios**:

1. **Given** host creates a new poll "K7F3", **When** they create another poll "M9P2", **Then** poll "K7F3" is automatically marked as closed
2. **Given** poll "K7F3" is closed, **When** participant navigates to `/p/K7F3`, **Then** they see the question and options but voting is disabled with "Poll closed" message
3. **Given** poll "K7F3" is closed with 15 votes, **When** viewing `/p/K7F3/results`, **Then** results show final vote distribution with "CLOSED" status prominently displayed
4. **Given** poll "K7F3" is closed, **When** participant attempts to submit a vote via API, **Then** request is rejected with appropriate error message
5. **Given** multiple closed polls exist, **When** host views dashboard, **Then** they can see list of all polls with their codes, questions, and closed status

---

### User Story 6 - QR Code Generation for Easy Join (Priority: P6)

Host receives a QR code pointing to the poll join URL, making it easy for audience members to join from their phones.

**Why this priority**: Significantly improves user experience in conference setting (easier than typing URLs), but poll can function without it using manual URL entry.

**Independent Test**: Can be tested by generating QR code for poll URL, scanning it with a phone camera, and successfully navigating to the poll voting page.

**Acceptance Scenarios**:

1. **Given** host creates poll "K7F3" with join URL `https://codespace-url.app.github.dev/p/K7F3`, **When** viewing poll details, **Then** a QR code image is displayed encoding the full join URL
2. **Given** QR code is displayed for poll "K7F3", **When** participant scans code with phone camera, **Then** they are directed to `/p/K7F3` voting page
3. **Given** Codespace URL changes (port forwarding update), **When** QR code is regenerated, **Then** it reflects the current public URL
4. **Given** host dashboard displays poll "K7F3", **When** viewing poll, **Then** QR code is large enough to scan from across a conference room (minimum 200x200 pixels)

---

### Edge Cases

- What happens when participant tries to vote on a non-existent poll code? System should show "Poll not found" error message
- How does system handle invalid poll codes (wrong format, special characters)? Validate code format and show friendly error
- What happens when poll has 0 votes and results page is accessed? Display should show 0% and "No votes yet" message
- How does system handle extremely long poll questions or option text? Truncate or wrap text appropriately for display, validate max length on input (question max 500 chars, options max 200 chars each)
- What happens if participant loses internet mid-vote? Vote submission fails gracefully with retry option or clear error message
- How does system handle rapid vote updates (100+ participants voting simultaneously)? SignalR should batch updates or throttle to prevent overwhelming clients (acceptable 2-5 second delay)
- What happens when host token is missing or invalid? Host-only endpoints return 401 Unauthorized with clear message
- How does system handle browser refresh on voting page after submitting vote? Previous vote should remain selected (idempotent behavior)
- What happens when creating poll with only 1 option? Validate minimum 2 options required
- What happens when creating poll with more than 6 options? Validate maximum 6 options

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow host to create a poll with a question, 2-6 answer options, and choice mode (single/multi)
- **FR-002**: System MUST generate a unique 4-character alphanumeric join code for each poll (e.g., "K7F3")
- **FR-003**: System MUST provide a join URL in format `/p/{code}` that participants can access without authentication
- **FR-004**: System MUST allow participants to submit votes by selecting one or more options (based on poll's choice mode)
- **FR-005**: System MUST track voter identity using browser cookies or session to prevent duplicate votes
- **FR-006**: System MUST allow participants to update their vote (replace previous vote, not add duplicate)
- **FR-007**: System MUST display live-updating results at `/p/{code}/results` showing vote counts and percentages
- **FR-008**: System MUST use SignalR for real-time result updates with fallback to periodic polling if SignalR unavailable
- **FR-009**: System MUST authenticate host-only actions using a host token provided via environment variable
- **FR-010**: System MUST accept host token via HTTP header `X-Host-Token` or host-only cookie
- **FR-011**: System MUST automatically close previous poll when host creates a new poll
- **FR-012**: System MUST prevent voting on closed polls while keeping results accessible
- **FR-013**: System MUST generate QR code image encoding the full poll join URL
- **FR-014**: System MUST persist poll data (polls, options, votes) in SQLite database
- **FR-015**: System MUST validate poll creation: minimum 2 options, maximum 6 options
- **FR-016**: System MUST validate text lengths: question max 500 characters, option max 200 characters
- **FR-017**: System MUST display results with large, projection-friendly fonts (minimum 24px for main content)
- **FR-018**: System MUST show total vote count on results page
- **FR-019**: System MUST display clear status indicators ("OPEN" or "CLOSED") on results page
- **FR-020**: System MUST provide host dashboard listing all polls with codes, questions, and status
- **FR-021**: System MUST run in GitHub Codespaces with publicly accessible forwarded port
- **FR-022**: System MUST handle vote submission failures gracefully with user-friendly error messages
- **FR-023**: System MUST preserve vote selections after browser refresh (idempotent behavior)
- **FR-024**: System MUST return 404 with user-friendly message for non-existent poll codes
- **FR-025**: System MUST return 401 Unauthorized when host token is missing or invalid for protected endpoints

### Key Entities

- **Poll**: Represents a conference poll with a unique code, question text, choice mode (single/multi), open/closed status, and timestamp. Related to multiple Options and Votes.
- **Option**: Represents one answer choice within a poll with text content. Belongs to exactly one Poll and can have multiple Votes.
- **Vote**: Represents a participant's selection with a voter identifier (cookie/session-based) linking to specific Option(s). Belongs to one Poll. For single-choice polls, one vote per voter; for multi-choice, multiple votes per voter allowed.
- **Voter**: Implicit entity representing a unique participant identified by browser cookie or session. Used to prevent duplicate votes and enable vote updates.

### UX Requirements *(per Constitution Principle III)*

- **UX-001**: Navigation patterns MUST be simple and minimal - primary flows are: host dashboard → create poll → view results, and participant: join URL → vote → see confirmation
- **UX-002**: Error messages MUST be actionable and user-friendly (e.g., "Poll not found. Check the code and try again" not "404 Error")
- **UX-003**: Loading states MUST be shown for operations >500ms (poll creation, vote submission, results loading)
- **UX-004**: MUST meet WCAG 2.1 AA accessibility standards (keyboard navigation for all actions, sufficient color contrast 4.5:1, screen reader support for form labels and buttons)
- **UX-005**: MUST be responsive across mobile phones (primary participant device), tablets, and desktop/projection displays (results view)
- **UX-006**: User feedback MUST include success confirmations ("Vote recorded!"), error states (submission failures), and validation messages (e.g., "Please select at least one option")
- **UX-007**: Results page MUST use high-contrast colors and large fonts optimized for projection viewing from distance
- **UX-008**: Join code MUST be displayed in large, easily readable format (uppercase, monospace font recommended)
- **UX-009**: QR code MUST be sized for scanning from across a conference room (minimum 200x200 pixels)
- **UX-010**: Poll voting page MUST clearly indicate choice mode (e.g., "Select one" vs "Select all that apply")

### Performance Requirements *(per Constitution Principle IV)*

- **PERF-001**: Poll creation API MUST complete within 500ms (includes code generation and database write)
- **PERF-002**: Vote submission API MUST complete within 300ms (p95 latency) to support rapid voting
- **PERF-003**: Results page load MUST complete initial render within 2s on 3G connections
- **PERF-004**: SignalR real-time updates MUST deliver new votes to results page within 2 seconds of submission
- **PERF-005**: Poll join page (voting page) MUST load within 1.5s on 3G connections (critical for participant experience)
- **PERF-006**: Database queries MUST use proper indexing on poll code lookups (primary access pattern)
- **PERF-007**: System MUST handle 100+ concurrent participants voting on same poll without degradation
- **PERF-008**: QR code generation MUST complete within 200ms (can be cached per poll)
- **PERF-009**: Results page MUST throttle real-time updates to max 1 update per second when handling rapid vote bursts (100+ votes/min)
- **PERF-010**: Host dashboard page load MUST complete within 1s (typically <10 polls to display)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Host can create a poll and receive join code + QR code within 10 seconds (from click to ready-to-share)
- **SC-002**: Participants can join and vote within 30 seconds from seeing QR code (scan → vote → confirmation)
- **SC-003**: Results update on projector display within 5 seconds of participant vote submission (including network delays)
- **SC-004**: System successfully handles a conference room of 100 participants voting within a 2-minute window without errors
- **SC-005**: Zero duplicate votes recorded when same participant votes multiple times (vote update/replacement works correctly)
- **SC-006**: Results remain accessible and viewable after poll is closed with no data loss
- **SC-007**: App runs successfully in GitHub Codespaces with public port forwarding requiring zero deployment configuration
- **SC-008**: Participants can successfully vote from mobile phones (iOS/Android) without responsive layout issues
- **SC-009**: Results page is readable from 20 feet away when projected (font sizes adequate for conference room)
- **SC-010**: 95% of participants can join and vote without assistance (intuitive UX validation)

### Assumptions

- GitHub Codespaces provides stable public port forwarding for the application's lifetime
- Conference WiFi provides basic connectivity (3G speeds or better) for participants
- Participants have modern mobile browsers (Chrome, Safari, Firefox from last 2 years)
- Host has access to a device for controlling polls (laptop/tablet with host token configured)
- SQLite provides sufficient performance for concurrent voting (100 participants is acceptable upper limit)
- Real-time updates via SignalR are a nice-to-have; periodic refresh fallback is acceptable
- Poll data persistence beyond Codespace lifetime is not required (data loss on container stop is acceptable)
- No requirement for poll editing after creation (delete and recreate is acceptable workflow)
- Host token security is adequate for conference demo environment (not production-grade auth)
- Single active poll at a time is sufficient (creating new poll closes previous is acceptable)

### Out of Scope

The following are explicitly excluded from this version:

- User accounts, login systems, or persistent user profiles beyond voter cookie
- Poll editing capabilities after creation (no updating questions/options)
- Multiple simultaneously active polls (only one poll open at a time)
- Advanced analytics, charts, or data export beyond viewing results
- Moderation capabilities, comment filtering, or content review
- Multi-host support or role-based permissions beyond single host token
- Production-grade security features (rate limiting, WAF, DDoS protection)
- Long-term data persistence or backup capabilities
- Mobile native apps (web-only, responsive design sufficient)
- Internationalization or multi-language support
- Poll scheduling or time-based auto-close features
- Integration with external systems (no APIs for third-party access)
- Results visualization beyond simple histogram/table (no pie charts, graphs)
- Participant authentication or identity verification beyond cookie
- Vote anonymity guarantees or audit trails
- Custom branding or white-labeling capabilities
