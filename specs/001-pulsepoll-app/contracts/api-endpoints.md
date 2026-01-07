# API Contracts: PulsePoll

**Feature**: PulsePoll - Conference Polling App  
**Phase**: 1 - Design  
**Date**: 2026-01-06

## Overview

This document defines the HTTP endpoints, request/response schemas, and validation rules for the PulsePoll application. Endpoints are organized by role (Host vs Participant).

---

## Host Endpoints (Authenticated)

All host endpoints require authentication via `X-Host-Token` header or `HostAuth` cookie.

### POST /host/polls - Create Poll

**Description**: Create a new poll with question and options. Automatically closes previous open poll.

**Authentication**: Required (host token)

**Request Body**:
```json
{
  "question": "What's your favorite color?",
  "choiceMode": "Single",  // or "Multi"
  "options": [
    { "text": "Red" },
    { "text": "Blue" },
    { "text": "Green" }
  ]
}
```

**Request Validation**:
- `question`: Required, 1-500 characters
- `choiceMode`: Required, must be "Single" or "Multi"
- `options`: Required array, 2-6 items
- `options[].text`: Required, 1-200 characters

**Response 201 Created**:
```json
{
  "pollId": 1,
  "code": "K7F3",
  "question": "What's your favorite color?",
  "choiceMode": "Single",
  "joinUrl": "/p/K7F3",
  "absoluteJoinUrl": "https://super-disco-abc123.app.github.dev/p/K7F3",
  "qrCodeDataUrl": "data:image/png;base64,iVBORw0KGgo...",
  "createdAt": "2026-01-06T14:30:00Z"
}
```

**Error Responses**:
- `400 Bad Request`: Validation errors
  ```json
  {
    "error": "Validation failed",
    "details": {
      "question": ["Question is required"],
      "options": ["Must provide 2-6 options"]
    }
  }
  ```
- `401 Unauthorized`: Missing or invalid host token
  ```json
  {
    "error": "Unauthorized",
    "message": "Host token is required. Please authenticate."
  }
  ```

**Performance**: Must complete within 500ms (PERF-001)

---

### GET /host/polls - List All Polls

**Description**: Retrieve all polls (open and closed) for host dashboard.

**Authentication**: Required (host token)

**Query Parameters**: None

**Response 200 OK**:
```json
{
  "polls": [
    {
      "pollId": 2,
      "code": "M9P2",
      "question": "Best session topic?",
      "isClosed": false,
      "totalVotes": 15,
      "createdAt": "2026-01-06T15:00:00Z",
      "resultsUrl": "/p/M9P2/results"
    },
    {
      "pollId": 1,
      "code": "K7F3",
      "question": "What's your favorite color?",
      "isClosed": true,
      "totalVotes": 42,
      "createdAt": "2026-01-06T14:30:00Z",
      "closedAt": "2026-01-06T15:00:00Z",
      "resultsUrl": "/p/K7F3/results"
    }
  ]
}
```

**Error Responses**:
- `401 Unauthorized`: Missing or invalid host token

**Performance**: Must complete within 1s (PERF-010)

---

### GET /host/polls/{code}/qr - Regenerate QR Code

**Description**: Generate new QR code for poll (useful if Codespaces URL changes).

**Authentication**: Required (host token)

**Path Parameters**:
- `code`: Poll code (4 alphanumeric characters)

**Response 200 OK**:
```json
{
  "pollCode": "K7F3",
  "qrCodeDataUrl": "data:image/png;base64,iVBORw0KGgo...",
  "absoluteJoinUrl": "https://new-url.app.github.dev/p/K7F3"
}
```

**Error Responses**:
- `404 Not Found`: Poll code doesn't exist
- `401 Unauthorized`: Missing or invalid host token

**Performance**: Must complete within 200ms (PERF-008)

---

## Participant Endpoints (Public)

No authentication required for participant endpoints.

### GET /p/{code} - View Poll (Voting Page)

**Description**: Display poll question and options with voting form.

**Path Parameters**:
- `code`: Poll code (4 alphanumeric characters, case-insensitive)

**Response 200 OK** (HTML page):
- Poll question
- Options (radio buttons for single-choice, checkboxes for multi-choice)
- Submit button
- Previous selections pre-selected if voter has already voted (via VoterId cookie)
- "Poll closed" message if poll is closed (voting disabled)

**Error Responses**:
- `404 Not Found`: Poll code doesn't exist
  - Display: "Poll not found. Check the code and try again."

**Performance**: Must load within 1.5s on 3G (PERF-005)

---

### POST /p/{code}/vote - Submit Vote

**Description**: Submit or update vote for a poll.

**Path Parameters**:
- `code`: Poll code

**Request Body** (form-encoded or JSON):
```json
{
  "selectedOptionIds": [2]  // Single-choice: 1 item, Multi-choice: 1-N items
}
```

**Request Validation**:
- `selectedOptionIds`: Required array, min 1 item
- For single-choice polls: Exactly 1 option
- For multi-choice polls: 1-N options (where N = total options)
- All option IDs must belong to the specified poll

**Response 200 OK**:
```json
{
  "success": true,
  "message": "Vote recorded!",
  "voterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"  // From cookie
}
```

**Response 303 See Other** (HTML form submission):
- Redirect to `/p/{code}` with success message
- Set `PulsePollVoterId` cookie if not already set

**Error Responses**:
- `400 Bad Request`: Validation errors
  ```json
  {
    "error": "Invalid vote",
    "message": "Please select at least one option"
  }
  ```
- `404 Not Found`: Poll code doesn't exist
- `409 Conflict`: Poll is closed
  ```json
  {
    "error": "Poll closed",
    "message": "This poll is no longer accepting votes"
  }
  ```

**Side Effects**:
- If VoterId cookie doesn't exist: Generate new GUID, set cookie (7-day expiration)
- If voter has existing votes for this poll: Delete old votes, insert new votes (atomic transaction)
- Broadcast update via SignalR: `ResultsHub.Clients.Group(pollCode).SendAsync("VoteUpdated")`

**Performance**: Must complete within 300ms p95 (PERF-002)

---

### GET /p/{code}/results - View Results (Results Page)

**Description**: Display live-updating poll results with vote counts and percentages.

**Path Parameters**:
- `code`: Poll code

**Response 200 OK** (HTML page):
- Poll question
- Poll status (OPEN or CLOSED)
- Total vote count
- Table/histogram of options with vote counts and percentages
- SignalR connection for live updates
- JavaScript fallback to polling `/api/results/{code}` every 5 seconds if SignalR fails

**Error Responses**:
- `404 Not Found`: Poll code doesn't exist

**Performance**: Must load within 2s on 3G (PERF-003)

---

### GET /api/results/{code} - Get Results (JSON API)

**Description**: Retrieve poll results as JSON for client-side rendering or fallback polling.

**Path Parameters**:
- `code`: Poll code

**Response 200 OK**:
```json
{
  "pollCode": "K7F3",
  "question": "What's your favorite color?",
  "isClosed": false,
  "totalVotes": 5,
  "options": [
    {
      "optionId": 1,
      "text": "Red",
      "voteCount": 3,
      "percentage": 60.0
    },
    {
      "optionId": 2,
      "text": "Blue",
      "voteCount": 2,
      "percentage": 40.0
    },
    {
      "optionId": 3,
      "text": "Green",
      "voteCount": 0,
      "percentage": 0.0
    }
  ]
}
```

**Error Responses**:
- `404 Not Found`: Poll code doesn't exist

**Performance**: Must complete within 200ms (constitution requirement for read operations)

---

## SignalR Hub: ResultsHub

### Connection

**Endpoint**: `/hubs/results`

**Client Connection**:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/results")
    .build();
```

### Methods

#### JoinPollGroup (Client → Server)

**Description**: Add client connection to poll-specific group for targeted broadcasts.

**Parameters**:
- `pollCode` (string): Poll code to join

**Invocation**:
```javascript
await connection.invoke("JoinPollGroup", "K7F3");
```

#### VoteUpdated (Server → Client)

**Description**: Broadcast triggered when new vote is submitted.

**Parameters**:
- `pollCode` (string): Poll code that received vote
- `totalVotes` (int): Updated total vote count

**Client Handler**:
```javascript
connection.on("VoteUpdated", (pollCode, totalVotes) => {
    // Fetch updated results from /api/results/{pollCode}
    // Re-render results display
});
```

**Throttling**: Max 1 broadcast per second during rapid voting (PERF-009)

---

## Request/Response Headers

### Common Headers

**All Requests**:
- `Content-Type: application/json` or `application/x-www-form-urlencoded`
- `Accept: application/json` or `text/html`

**Host Endpoints**:
- `X-Host-Token: <token>` (alternative to cookie)
- `Cookie: HostAuth=<encrypted-token>` (alternative to header)

**Participant Endpoints**:
- `Cookie: PulsePollVoterId=<guid>` (set after first vote)

### CORS

Not required - all requests from same origin (Codespaces public URL).

---

## Error Response Format

All error responses follow consistent structure:

```json
{
  "error": "ErrorType",
  "message": "User-friendly error message with recovery action",
  "details": {
    "field1": ["Validation error 1", "Validation error 2"],
    "field2": ["Validation error"]
  }  // Optional, only for validation errors
}
```

**HTTP Status Codes**:
- `200 OK`: Successful request
- `201 Created`: Poll created successfully
- `303 See Other`: Form submission redirect
- `400 Bad Request`: Validation errors
- `401 Unauthorized`: Authentication required
- `404 Not Found`: Resource doesn't exist
- `409 Conflict`: Business logic conflict (e.g., poll closed)
- `500 Internal Server Error`: Unexpected server error

---

## Authentication Flow

### Host Authentication

1. **Initial Setup**: Host token configured in `appsettings.json` or environment variable `HOST_TOKEN`
2. **Login** (optional endpoint `/host/login`):
   - POST `{ "token": "<host-token>" }`
   - If valid: Set `HostAuth` cookie (encrypted, HttpOnly, Secure, 24h expiration)
3. **Subsequent Requests**: Include `X-Host-Token` header OR `HostAuth` cookie
4. **Middleware Validation**: `HostAuthMiddleware` checks header/cookie, sets `HttpContext.User`

### Voter Identity

1. **First Vote**: No `PulsePollVoterId` cookie exists
   - Generate new GUID
   - Set cookie: `PulsePollVoterId=<guid>; Expires=+7days; SameSite=Lax`
2. **Subsequent Votes**: Cookie present
   - Read GUID from cookie
   - Use for duplicate detection
3. **Cookie Loss**: Treated as new voter (acceptable per spec assumptions)

---

## Data Flow Summary

### Create Poll → Share → Vote → View Results

```
1. Host POST /host/polls
   → Server generates code "K7F3", auto-closes old poll, returns QR code
   
2. Host displays QR code on screen
   → Participants scan QR, navigate to https://.../p/K7F3
   
3. Participant POST /p/K7F3/vote with selectedOptionIds=[2]
   → Server saves vote, sets VoterId cookie, broadcasts SignalR update
   
4. Results page /p/K7F3/results
   → SignalR receives "VoteUpdated" event
   → JavaScript fetches /api/results/K7F3
   → Updates DOM with new counts
```

---

## Performance SLA

| Endpoint | Target Latency | Spec Reference |
|----------|----------------|----------------|
| POST /host/polls | <500ms | PERF-001 |
| POST /p/{code}/vote | <300ms (p95) | PERF-002 |
| GET /p/{code} | <1.5s (3G) | PERF-005 |
| GET /p/{code}/results | <2s (3G) | PERF-003 |
| SignalR VoteUpdated | <2s latency | PERF-004 |
| GET /api/results/{code} | <200ms | Constitution |
| GET /host/polls | <1s | PERF-010 |

---

## Contract Testing

All endpoints will be validated with contract tests (see `tests/Contract/` in project structure) to ensure:
- Request/response schemas match specifications
- Validation rules are enforced
- Error responses follow standard format
- Performance targets are met (load testing)
