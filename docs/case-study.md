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

### Engineering evidence for the employer description

Dexter requested retaining these points on 19 October 2026 for the later demo description. Describe the stack as React/TypeScript, C#/ASP.NET Core, EF Core/Npgsql and PostgreSQL, packaged in Docker and hosted on Deplexo/Neon. Link to the code and actual results rather than using engineering terms without examples.

| Topic | What the demo demonstrates | Evidence to link |
| --- | --- | --- |
| CI/CD | GitHub Actions checks formatting, API/browser behaviour and the packaged Docker build. The current local suite passes 57 API/security and twelve browser tests. Only passing main builds may deploy; package checksum and live revision identify the release. A successful production run requires its deployment and live smoke result in the Actions summary. | Delivery workflow, PR checks, successful release run and live `/release.json`. |
| OWASP-aware API security | Ownership/expiry enforcement, typed commands, CSRF/Secure cookies, request/resource limits, controlled errors and repeat/attempt guards mapped to relevant OWASP API risks. | Architecture review, negative API tests and hosted checks. This is not an OWASP certification or a complete penetration test. |
| Browser security | Enforced CSP permits the app's own assets/API and blocks inline code, eval, external resources and framing. HSTS teaches browsers to use HTTPS for this host after a secure visit, with a bounded initial one-day policy. | ApiSafety/Program, Production policy/proxy tests, browser rejection tests and a full desktop/phone journey with no CSP violations. The release smoke check requires live CSP/HSTS headers. |
| SOLID principles applied proportionately | Single-responsibility separation between HTTP routing, workflow transactions, pure rules and view mapping; time and database dependencies supplied through the existing DI container. | WorkflowEndpoints, WorkflowService, WorkflowRules, WorkflowViewMapper and regression tests. Do not claim all five SOLID principles were formally assessed or add unused interfaces to support a slogan. |

The full-stack workflow and safe repeat/recovery behaviour are the central evidence. Email/register integration remains simulated; no production customer outcome, uptime guarantee or business saving is claimed.

The work began with an assessed and preserved earlier prototype. Useful React/assets were retained while the workflow moved from Worker/D1 code to the selected C#/PostgreSQL stack.

## Evidence and limits

The local harness passes **57 API/security tests and 12 Chromium browser tests**, covering the PostgreSQL-backed main journey, recovery, isolation, repeats, rollback, lost responses, Production proxy boundaries, API resource/error controls and CSP/HSTS. Desktop and phone walkthroughs were reviewed against the written acceptance criteria. Bounded hosted checks also exercise the real Deplexo/Neon workflow; see [HARNESS.md](../HARNESS.md) for evidence and limits.

Employer wording: "Added and tested browser security policies alongside server-enforced visitor isolation and workflow rules. GitHub Actions verifies the PostgreSQL-backed application, builds the checked source package, deploys only passing main revisions and checks the live release and security headers."

Security headers do not clear antivirus reputation lists. Dexter submitted a Webroot review request; vendor clearance remains unconfirmed. The exact hostname's Google Safe Browsing report showed "No unsafe content found" on 19 October 2026. Neither result is a security certification or a promise that all visitors will receive no warnings.

This is a deployed portfolio demonstration with fictional data, not a production business service. Email and the external register connection are simulated. Extended-idle startup, first-time human timing and employer-access reputation remain open; no measured business savings or real external integration is claimed. Employer distribution remains behind review.
