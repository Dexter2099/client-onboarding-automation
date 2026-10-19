# OnboardFlow: from signed scope to an approved kickoff

Draft for review · 19 October 2026 · Personal portfolio project

## The problem

A signed website project still needs usable client information. Submitted answers can be incomplete or outside the agreement, and receiving a reply does not mean it has been approved.

OnboardFlow demonstrates this with fictional Juniper Studio. The employer takes the project manager role. Maya's reply adds Shop to a signed five-page website, so the project manager must request a correction before kickoff.

## The application

The React/TypeScript workspace uses a C# ASP.NET Core API and PostgreSQL. The project manager prepares onboarding, reviews each answer, records a correction comment and approves a new Pages revision. Earlier answers and decisions remain visible; unchanged approvals are retained.

Explicit readiness saves an approved kickoff brief and closes the review task. Publishing then stores a separately confirmed, clearly labelled simulated register entry. Outbox and Activity show what was prepared, who acted and the result.

## Engineering choices

- One same-origin application and direct EF Core keep the structure small.
- Revision-bound decisions prevent a changed answer inheriting old approval.
- Transactions, unique constraints and command/version checks make repeats reuse saved work.
- A failed welcome preserves setup. Recovery allows three attempts total per project, including the first; restoring availability does not reset the budget.
- Private, fixed 24-hour sandboxes isolate visitors and support an explicit reset.

The work began with an assessed and preserved earlier prototype. Useful React/assets were retained while the workflow moved from Worker/D1 code to the selected C#/PostgreSQL stack.

## Evidence and limits

The local harness passes **50 API/security tests and 11 Chromium browser tests**, covering the PostgreSQL-backed main journey, recovery, isolation, repeats, rollback, lost responses, Production proxy boundaries and API resource/error controls. Desktop and phone walkthroughs were reviewed against the written acceptance criteria. Bounded hosted checks also exercise the real Deplexo/Neon workflow; see [HARNESS.md](../HARNESS.md) for evidence and limits.

This is a deployed portfolio demonstration with fictional data, not a production business service. Email and the external register connection are simulated. Extended-idle startup, first-time human timing and employer-access reputation remain open; no measured business savings or real external integration is claimed. Employer distribution remains behind review.
