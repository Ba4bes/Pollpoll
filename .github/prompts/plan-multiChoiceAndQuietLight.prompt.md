# Plan: Add Multi-Choice Poll Creation & Accessible Quiet Light Design

The host portal is missing the UI control to select between single-choice and multi-choice polls. All polls are currently hardcoded to single-choice mode. The results page needs to display a pie chart for single-choice polls (with table beneath) while keeping bar charts for multi-choice polls. Update the entire application's color scheme to match VS Code's Quiet Light theme with **WCAG AA accessibility compliance** throughout.

## Steps

1. **Define high-contrast Quiet Light color palette** — Create CSS custom properties in [site.css](PollPoll/wwwroot/css/site.css) based on Quiet Light theme with **verified WCAG AA contrast ratios** (minimum 4.5:1 for text, 3:1 for UI components), defining 6 chart colors with strong differentiation (ordered: deep blue #0066CC, forest green #008000, deep purple #6A1B9A, burnt orange #E65100, teal #00796B, magenta #C2185B), and ensuring all text/background combinations meet accessibility standards.

2. **Add choice mode selector to poll creation form** — Update [Index.cshtml](PollPoll/Pages/Index.cshtml) to include radio buttons for "Single Choice" vs "Multiple Choice" with clear labels and focus indicators, and update [Index.cshtml.cs](PollPoll/Pages/Index.cshtml.cs) to add `[BindProperty] public ChoiceMode ChoiceMode` property and pass it to `PollService.CreatePollAsync()`.

3. **Extend results model with choice mode** — Update `PollResults` model in [PollResults.cs](PollPoll/Models/PollResults.cs) to include a `ChoiceMode` property, and modify [ResultsService.cs](PollPoll/Services/ResultsService.cs) to populate this field from the poll entity.

4. **Add accessible Chart.js pie chart** — Update [Results.cshtml](PollPoll/Pages/Results.cshtml) to include Chart.js CDN, create canvas with proper ARIA labels for screen readers, configure pie chart with the high-contrast color array and legend, ensure keyboard navigation support, and update SignalR to refresh chart data while maintaining accessibility.

5. **Conditionally render accessible table styles** — Modify [Results.cshtml](PollPoll/Pages/Results.cshtml) to show high-contrast progress bars only for multi-choice polls and bold percentage text for single-choice polls, and update [results.css](PollPoll/wwwroot/css/results.css) with accessible chart container, visible focus indicators, and sufficient color contrast.

6. **Apply accessible styling site-wide** — Update [_Layout.cshtml](PollPoll/Pages/Shared/_Layout.cshtml), [Vote.cshtml](PollPoll/Pages/Vote.cshtml), [Archive.cshtml](PollPoll/Pages/Archive.cshtml), and all pages with high-contrast Quiet Light colors, ensuring all interactive elements have clear focus states and proper ARIA labels.
