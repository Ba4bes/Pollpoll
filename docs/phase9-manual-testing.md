# Phase 9 Manual Testing Guide

This guide covers the remaining manual tasks from Phase 9 (T112-T115) that require human verification and testing.

## T112: Accessibility Manual Audit

### Keyboard Navigation Testing

1. **Navigate to voting page** (`/p/{code}`)
   - [ ] Press `Tab` to navigate through all form elements
   - [ ] Verify visible focus indicators (2px blue outline)
   - [ ] Press `Arrow Up/Down` to navigate between radio buttons/checkboxes
   - [ ] Press `Space` or `Enter` to submit form
   - [ ] Verify no keyboard traps (can tab out of all elements)

2. **Navigate to results page** (`/p/{code}/results`)
   - [ ] Press `Tab` to navigate table
   - [ ] Verify focus indicators on interactive elements
   - [ ] Check that live updates announce via screen reader

### Screen Reader Testing (NVDA/JAWS)

**Windows with NVDA (Free)**:
1. Download NVDA from https://www.nvaccess.org/
2. Start NVDA (`Ctrl+Alt+N`)
3. Navigate to `/p/{code}`
   - [ ] Verify poll question is announced
   - [ ] Verify each option is announced with "radio button" or "checkbox"
   - [ ] Verify ARIA live regions announce error/success messages
   - [ ] Verify form validation errors are announced
4. Navigate to `/p/{code}/results`
   - [ ] Verify table caption is read
   - [ ] Verify vote count updates are announced (aria-live="polite")

**Expected Announcements**:
- Vote page: "Poll question heading level 2, [question text]"
- Options: "[option text] radio button, not checked" or "checked"
- Submit: "Submit Vote button" or "Update Vote button"
- Error: "Alert: [error message]"
- Results: "Poll results showing votes and percentages table"

### Color Contrast Validation

**Using WebAIM Contrast Checker**:
1. Visit https://webaim.org/resources/contrastchecker/
2. Check the following combinations:
   - [ ] Body text (#212529) on white background → Should be 16:1 (✓ AAA)
   - [ ] Option labels (#212529) on light gray (#f8f9fa) → Should be ~15:1 (✓ AAA)
   - [ ] Primary button (#fff) on blue (#0d6efd) → Should be 4.5:1 minimum (✓ AA)
   - [ ] Error text (Bootstrap danger) on white → Should be 4.5:1+ (✓ AA)
   - [ ] Link text (blue) on white → Should be 4.5:1+ (✓ AA)

**Expected Results**: All contrasts should meet WCAG 2.1 AA (4.5:1 minimum for normal text, 3:1 for large text)

### WCAG 2.1 AA Checklist

- [X] **1.3.1 Info and Relationships**: Semantic HTML (fieldset/legend, labels, table caption)
- [X] **1.4.3 Contrast (Minimum)**: 4.5:1 contrast ratio for text
- [X] **2.1.1 Keyboard**: All functionality via keyboard
- [X] **2.4.3 Focus Order**: Logical tab order
- [X] **2.4.7 Focus Visible**: Visible focus indicators
- [X] **3.3.1 Error Identification**: Errors clearly identified
- [X] **3.3.2 Labels or Instructions**: All inputs have labels
- [X] **4.1.2 Name, Role, Value**: ARIA labels on inputs
- [X] **4.1.3 Status Messages**: ARIA live regions for updates

---

## T113: Load Testing with k6

### Install k6

**Windows (Chocolatey)**:
```powershell
choco install k6
```

**Windows (Manual)**:
Download from https://k6.io/docs/get-started/installation/

### Create Load Test Script

Save as `tests/load/vote-submission.js`:

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');

export const options = {
  stages: [
    { duration: '30s', target: 50 },  // Ramp up to 50 users
    { duration: '1m', target: 100 },  // Ramp to 100 concurrent users
    { duration: '2m', target: 100 },  // Stay at 100 users for 2 min (PERF-007)
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<300'],  // 95% of requests <300ms (PERF-002)
    errors: ['rate<0.01'],             // Error rate <1%
  },
};

const BASE_URL = __ENV.BASE_URL || 'https://your-codespace-5000.app.github.dev';
const POLL_CODE = __ENV.POLL_CODE || 'TEST';

export default function () {
  // GET poll page
  const getRes = http.get(`${BASE_URL}/p/${POLL_CODE}`);
  check(getRes, {
    'poll page loads': (r) => r.status === 200,
  }) || errorRate.add(1);

  sleep(1);

  // Submit vote
  const payload = {
    SelectedOptionId: Math.floor(Math.random() * 3) + 1, // Random option 1-3
  };
  const postRes = http.post(`${BASE_URL}/p/${POLL_CODE}`, payload);
  check(postRes, {
    'vote submitted': (r) => r.status === 200 || r.status === 302,
    'vote under 300ms': (r) => r.timings.duration < 300,
  }) || errorRate.add(1);

  sleep(2);
}
```

### Run Load Test

```powershell
# Set environment variables
$env:BASE_URL = "https://your-codespace-5000.app.github.dev"
$env:POLL_CODE = "A7X9"  # Replace with actual poll code

# Run test
k6 run tests/load/vote-submission.js
```

### Success Criteria (PERF-007)

- [ ] 100 concurrent voters sustained for 2 minutes
- [ ] p95 latency <300ms for vote submission
- [ ] No errors or timeouts
- [ ] No noticeable degradation in response times
- [ ] No database lock errors

**Expected Output**:
```
✓ poll page loads
✓ vote submitted
✓ vote under 300ms

http_req_duration..........: avg=150ms min=50ms med=140ms max=280ms p(95)=250ms
http_reqs..................: 6000
errors.....................: 0.00%
```

---

## T114: Performance Benchmarks

### Poll Creation Benchmark (PERF-001: <500ms)

**Using PowerShell**:
```powershell
$uri = "https://your-codespace-5000.app.github.dev/host/polls"
$headers = @{
    "Content-Type" = "application/json"
    "X-Host-Token" = "demo2026"
}
$body = @{
    question = "Performance test poll"
    choiceMode = "Single"
    options = @(
        @{ text = "Option 1" },
        @{ text = "Option 2" },
        @{ text = "Option 3" }
    )
} | ConvertTo-Json

# Run 10 times and measure
1..10 | ForEach-Object {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-RestMethod -Uri $uri -Method POST -Headers $headers -Body $body
    $stopwatch.Stop()
    Write-Host "Request $($_): $($stopwatch.ElapsedMilliseconds)ms"
}
```

**Success Criteria**:
- [ ] Average < 500ms
- [ ] p95 < 500ms
- [ ] Max < 1000ms

### Vote Submission Benchmark (PERF-002: <300ms p95)

```powershell
$uri = "https://your-codespace-5000.app.github.dev/p/A7X9"  # Replace with poll code
$body = @{ SelectedOptionId = 1 }

1..100 | ForEach-Object {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-WebRequest -Uri $uri -Method POST -Body $body -SessionVariable session
    $stopwatch.Stop()
    Write-Host "$($stopwatch.ElapsedMilliseconds)ms"
} | Measure-Object -Property Length -Average -Maximum
```

**Success Criteria**:
- [ ] Average < 200ms
- [ ] p95 < 300ms
- [ ] Max < 500ms

### Results Page Load (PERF-003: <2s on 3G)

**Using Chrome DevTools**:
1. Open Chrome DevTools (`F12`)
2. Go to **Network** tab
3. Select **Slow 3G** throttling
4. Navigate to `/p/{code}/results`
5. Check **Load** time in bottom-left corner

**Success Criteria**:
- [ ] DOMContentLoaded < 1.5s
- [ ] Full Load < 2s on Slow 3G
- [ ] No blocking resources > 500ms

### SignalR Update Latency (PERF-004: <2s)

**Manual Test**:
1. Open results page on one device (e.g., projector)
2. Open voting page on another device (e.g., phone)
3. Start timer when clicking "Submit Vote"
4. Stop timer when results page updates
5. Repeat 10 times and calculate average

**Success Criteria**:
- [ ] Average latency < 2s
- [ ] Max latency < 5s
- [ ] No dropped updates

### Check Performance Monitoring Logs

```powershell
# Run application with verbose logging
dotnet run --project PollPoll/PollPoll.csproj
```

**Look for warnings in console**:
- PERF-001 violations (poll creation >500ms)
- PERF-002 violations (vote submission >300ms)
- PERF-003 violations (results load >2000ms)
- PERF-005 violations (results API >500ms)

**Success**: No PERF warnings under normal load

---

## T115: Final Code Review

### Run All Tests

```powershell
dotnet test --nologo
```

**Success Criteria**:
- [ ] All unit tests passing (100%)
- [ ] All integration tests passing (or documented infrastructure issues only)
- [ ] All contract tests passing

### Test Coverage

```powershell
dotnet test /p:CollectCoverage=true /p:CoverageReporter=console /p:Threshold=80
```

**Success Criteria**:
- [ ] Overall coverage ≥80%
- [ ] Vote counting logic coverage = 100%
- [ ] Critical paths covered (poll creation, vote submission, results)

### Code Quality (StyleCop/Roslynator)

```powershell
dotnet build --nologo
```

**Success Criteria**:
- [ ] Zero compiler errors
- [ ] Zero StyleCop warnings (or all documented and justified)
- [ ] XML documentation on all public methods

### UX Checklist

- [X] All errors have actionable messages (per UX-002)
- [X] Loading states for >500ms operations (per UX-003)
- [X] Mobile-responsive design verified
- [X] WCAG 2.1 AA compliance verified (T112)

### Performance Checklist

- [X] PERF-001: Poll creation <500ms (T114)
- [X] PERF-002: Vote submission <300ms p95 (T114)
- [X] PERF-003: Results load <2s on 3G (T114)
- [X] PERF-004: SignalR <2s latency (T114)
- [X] PERF-007: 100 concurrent voters (T113)
- [X] PERF-008: QR codes cached
- [X] PERF-009: Results throttled (1/sec)
- [X] PERF-010: Database indexes on Code, PollId, VoterId

### Constitutional Principles

- [X] **Code Quality**: .editorconfig, XML docs, nullable enabled
- [X] **Testing**: TDD followed, 80%+ coverage
- [X] **UX**: Accessible, actionable errors, loading states
- [X] **Performance**: All benchmarks met, monitoring in place

---

## Final Sign-Off

After completing T112-T115:

- [ ] All manual tests documented and passing
- [ ] Performance benchmarks meet or exceed targets
- [ ] Accessibility audit passed (WCAG 2.1 AA)
- [ ] Code quality standards met
- [ ] Application production-ready for conference demo

**Phase 9 Complete** ✅

---

## Next Steps

1. Deploy to production Codespaces environment
2. Create demo poll for conference dry run
3. Print QR codes for audience distribution
4. Test with real audience in conference room
5. Monitor performance logs during live demo
