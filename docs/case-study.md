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
| CI/CD | GitHub Actions checks formatting, unit/API/browser behaviour and the packaged Docker build. The suite contains 16 unit cases, 57 integration/security cases and 36 browser executions: twelve scenarios in each of Chromium, Firefox and WebKit. Only passing main builds may deploy; package checksum and live revision identify the release. A successful production run requires its deployment and live smoke result in the Actions summary. | Delivery workflow, PR checks, successful release run and live `/release.json`. |
| OWASP-aware API security | Ownership/expiry enforcement, typed commands, CSRF/Secure cookies, request/resource limits, controlled errors and repeat/attempt guards mapped to relevant OWASP API risks. | Architecture review, negative API tests and hosted checks. This is not an OWASP certification or a complete penetration test. |
| Browser security | Enforced CSP permits the app's own assets/API and blocks inline code, eval, external resources and framing. HSTS teaches browsers to use HTTPS for this host after a secure visit, with a bounded initial one-day policy. | ApiSafety/Program, Production policy/proxy tests, browser rejection tests and a full desktop/phone journey with no CSP violations. The release smoke check requires live CSP/HSTS headers. |
| Browser compatibility | The same main, recovery, isolation, keyboard and responsive-layout scenarios run in three browser engines. A separate post-deploy WebKit job checks the live main journey on native macOS at 2x scale. No visitor-side install or third-party cookie dependency is needed. | Playwright projects, per-engine reports/screenshots and CI. The Mac job's actual result is separate from Windows/Ubuntu regression; branded Safari and physical iOS certification are not claimed. |
| SOLID principles applied proportionately | Single-responsibility separation between HTTP routing, workflow transactions, pure rules and view mapping; time and database dependencies supplied through the existing DI container. | WorkflowEndpoints, WorkflowService, WorkflowRules, WorkflowViewMapper and regression tests. Do not claim all five SOLID principles were formally assessed or add unused interfaces to support a slogan. |

The full-stack workflow and safe repeat/recovery behaviour are the central evidence. Email/register integration remains simulated; no production customer outcome, uptime guarantee or business saving is claimed.

The work began with an assessed and preserved earlier prototype. Useful React/assets were retained while the workflow moved from Worker/D1 code to the selected C#/PostgreSQL stack.

## QA and testing evidence

The strategy prioritises the demo's failure risks: approving unsigned scope or a stale answer, leaking another visitor's work, duplicating outputs on repeat clicks and claiming success after a failed write. Test levels describe how much of the system runs. Regression, security and recovery describe why a test runs; these categories overlap and must not be added together as separate totals. Using xUnit does not make an HTTP/database test a unit test. This distinction follows Microsoft's [unit/integration testing guidance](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).

| Test type / purpose | Concrete evidence in this demo | Boundary |
| --- | --- | --- |
| Unit | [WorkflowRulesTests](../tests/OnboardFlow.Api.Tests/WorkflowRulesTests.cs): sixteen isolated cases for missing/extra/duplicate/case-changed pages, valid reordered scope, incomplete answers, welcome prerequisites and current-revision approval. | No HTTP host, EF/database fixture, network, container or mock framework. Internal rules stay internal; only the existing test assembly receives access. |
| Integration / API / database | [SandboxTests](../tests/OnboardFlow.Api.Tests/SandboxTests.cs), [WorkflowTests](../tests/OnboardFlow.Api.Tests/WorkflowTests.cs) and [WorkflowReliabilityTests](../tests/OnboardFlow.Api.Tests/WorkflowReliabilityTests.cs) exercise real ASP.NET requests, EF Core, migrations and disposable PostgreSQL. [ApiSafetyTests](../tests/OnboardFlow.Api.Tests/ApiSafetyTests.cs) and [ProxyTests](../tests/OnboardFlow.Api.Tests/ProxyTests.cs) cover the Production middleware pipeline. | Fifty-seven cases. Pipeline-only checks do not need database writes; database cases never use hosted/development data. |
| End-to-end (E2E) | [Playwright browser suite](../tests/browser): correction, fresh approval, frozen brief, confirmed handoff, repeat and saved state through the built React UI and real local API/database. | Twelve scenarios repeated in three engines: 36 executions, not 36 distinct behaviours. External email/register operations remain explicitly simulated. |
| Smoke / deployment verification | [API smoke](../scripts/smoke-demo.mjs) checks the exact live revision, HTTPS policies and database-backed visitor Start/repeat. [macOS smoke](../scripts/smoke-macos.mjs) completes the hosted visitor UI journey after deployment. | Small bounded release checks, with no deployment credentials in the Mac job or automatic mutation retries. A successful smoke does not prove long-term uptime. |
| Regression | The same unit, integration and browser suite runs on PRs and main; failing verification blocks deployment. | Regression is reuse of the suites above to catch breakage, not another count or a claim that every behaviour is covered. |
| Security / negative / boundary | Other-visitor IDs, forged/missing credentials, CSRF, exact expiry, stale revisions, body/rate limits, invalid proxy evidence and real browser CSP rejection. | OWASP-aware controls and focused abuse cases; no independent penetration test, full ASVS assessment or antivirus-reputation clearance. |
| Concurrency / repeat safety / recovery | Overlapping Start/Reset/Publish, replayed commands, lost responses, three-attempt exhaustion and injected transaction failures verify reuse, rollback and retained work. Additive-migration checks retain existing successful work. | Bounded concurrency and controlled failure injection; these are not sustained-load, stress or disaster-recovery certification. |
| Compatibility / responsive layout | Chromium, Firefox and WebKit on Windows/Ubuntu; a successful native macOS arm64/WebKit run on the deployed app, 2x pixel scale and desktop/phone widths. | Mac evidence is [run 39192306214](https://github.com/Dexter2099/client-onboarding-automation/actions/runs/39192306214). Branded Safari, physical iOS/Android and every older/browser-managed setup remain unverified. |
| Accessibility / usability | Browser assertions check readable text, overflow, keyboard project controls and a contextual route to review. Screenshots receive visual review. | Partial checks, not a full accessibility/WCAG audit. Screen readers, zoom/contrast audit and a first-time human timed walkthrough remain open. Automation cannot establish human understanding. |
| Performance / endurance | HARNESS.md retains observed local resource/hosted response probes. | No load/stress/soak suite, capacity claim or guaranteed latency. Extended-idle/overnight behaviour remains unmeasured; no keep-alive workaround is used. |

`npm run verify` is the reproducible full regression command. The existing test project labels C# cases with `Category=Unit` or `Category=Integration`; the unit filter documented in README runs without connection strings. CI retains TRX, browser reports/screenshots and native Mac result JSON for seven days. The workflow/run links retain the durable commit and pass/fail history after those artifacts expire. A failure prevents release verification; it is investigated rather than hidden by a blind retry. No percentage-coverage target is claimed without measurement.

Employer wording: "Built a risk-based QA suite with isolated rule tests, PostgreSQL-backed API integration tests and browser journeys across three engines. Tested visitor isolation, stale approvals, concurrency, rollback and lost-response recovery; GitHub Actions gates deployment and verifies the live release, including a native macOS WebKit smoke test."

## Evidence and limits

The local harness passes **73 C# cases (16 unit and 57 integration/security) and 36 browser executions** (twelve scenarios per engine in Chromium, Firefox and WebKit), covering the PostgreSQL-backed main journey, recovery, isolation, repeats, rollback, lost responses, Production proxy boundaries, API resource/error controls and CSP/HSTS. Desktop and phone layouts were reviewed against the written acceptance criteria. Bounded hosted checks also exercise the real Deplexo/Neon workflow; see [HARNESS.md](../HARNESS.md) for evidence and limits. Local regression uses Windows and CI uses Ubuntu; [run 39192306214](https://github.com/Dexter2099/client-onboarding-automation/actions/runs/39192306214) additionally passed native macOS arm64/WebKit 27.2 live verification of revision `10c8399cadd845707c879d1585fd322ccdc43bc6`. Branded Safari, physical phones and assistive-technology testing remain unverified; a build's syntax targets do not prove every older browser version works.

Employer wording: "Added and tested browser security policies alongside server-enforced visitor isolation and workflow rules. GitHub Actions verifies the PostgreSQL-backed application, builds the checked source package, deploys only passing main revisions and checks the live release and security headers."

Security headers do not clear antivirus reputation lists. Dexter submitted a Webroot review request; vendor clearance remains unconfirmed. The exact hostname's Google Safe Browsing report showed "No unsafe content found" on 19 October 2026. Neither result is a security certification or a promise that all visitors will receive no warnings.

This is a deployed portfolio demonstration with fictional data, not a production business service. Email and the external register connection are simulated. Extended-idle startup, first-time human timing and employer-access reputation remain open; no measured business savings or real external integration is claimed. Employer distribution remains behind review.
