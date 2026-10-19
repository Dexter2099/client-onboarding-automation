# OnboardFlow project plan

Updated 19 October 2026. **Status: the hosted workspace failure is resolved; bounded Deplexo/Neon workflow and security checks pass.** The app uses the verified Cloudflare scheme from its exact configured proxy, retaining Production antiforgery and Secure cookies. Both accounts remain Free. Hour-idle/overnight entry, first-time human timing and employer-access reputation remain open. No card, paid upgrade or portfolio-site change is authorised. Dexter's subsequent instruction authorises GitHub source publication and CI/CD for the existing app. See [HARNESS.md](HARNESS.md).

Dexter subsequently requested the explanatory introduction and a focused quality/API review. That bounded slice passes 50 local API/security tests, eleven browser tests and 54 live workflow/API requests. It separates existing responsibilities without schema/dependency changes. At his request, do not prolong this handoff with hours of manual testing; retain long-idle, sustained uptime and first-time human timing as unmeasured limits. Portfolio edits/publication remain with the other chat, explicitly reconfirmed on 19 October 2026. He then explicitly requested GitHub deployment and meaningful employer-visible CI/CD: test the commit, package/build its source, deploy only after success and verify the live revision. This supersedes the earlier source-publication hold, without reopening product planning.

## Purpose and document ownership

OnboardFlow is a client-onboarding workspace for a small web agency. A project manager takes an already signed project from missing client information to an approved kickoff brief and an inspectable handoff.

Its primary portfolio audience is employers hiring junior software and full-stack developers. The evidence should be a useful React interface, a C# API, relational data, meaningful tests and decisions Dexter can explain. Integration and QA skills support that story. This remains a personal project, not commercial employment experience.

Keep five working documents. This file owns scope, business rules, acceptance and decisions; the others elaborate their own subjects without redefining the product.

The [README](README.md), [case study](docs/case-study.md) and [recording script](docs/demo-recording-script.md) are the authorised release/presentation artifacts. They do not create extra planning authorities or replace these five working documents.

| Document | Owns |
| --- | --- |
| PLAN.md | Product purpose, scope, acceptance criteria, decisions and backlog. |
| [Employer walkthrough](docs/employer-walkthrough.md) | Visitor journey, screen hierarchy and interaction states. |
| [Architecture and existing code](docs/architecture.md) | Current baseline, reuse assessment and intended technical boundaries. |
| [HARNESS.md](HARNESS.md) | Development loop, milestone boundaries, commands and verification evidence. |
| [AGENTS.md](AGENTS.md) | Short repository instructions for coding agents. |

The dated [developer research](docs/seek-developer-research-2026-10-18.md) and [automation research](docs/seek-automation-research-2026-10-18.md) remain supporting evidence, not additional plans. Do not rewrite their historical conclusions as current decisions.

## Confirmed direction and existing work

- Dexter selected **React, TypeScript, C#, ASP.NET Core and PostgreSQL** on 19 October 2026.
- Dexter accepted the same-origin ASP.NET Core/React approach with .NET 10 LTS, EF Core/Npgsql and PostgreSQL on 19 October 2026. The architecture document owns the working data/state design.
- This work improves an existing initial codebase. Assess it before deciding what to retain, repair or replace; do not assume either a complete rewrite or that its old design must survive.
- The project must cost Dexter nothing extra and offer a public employer demo without visitor registration or personal data.
- The employer enters from the OnboardFlow card on **dexter-developer.dev**, using **Try this demo** beside **GitHub**. The demo opens separately on the prepared project screen. A provider HTTPS address is sufficient; a custom subdomain is optional. This entry design does not require the portfolio host to run C# or PostgreSQL. It does not replace the working app with a recording or browser-only simulation.
- On 19 October 2026 Dexter clarified that free hosting must also require **no credit-card verification**. Reject providers requiring a card before setup; do not ask him to add one to unlock a free tier. He confirmed no existing server or spare computer is available for self-hosting.
- On 19 October 2026 Dexter ruled out hosting that imposes an approximately minute-long startup wait after inactivity. Validate usable cold-entry performance as well as the warm walkthrough.
- Keep portfolio-site changes in the other chat. Review locally before any deployment.

The earlier prototype was located in local Git tree `01505e94b0c309ba8138dffe8bb4ce7846ab1d54` and preserved under local tag `codex/legacy-prototype-2026-10-19`. A `v1-archive` branch was unavailable during inspection. On 19 October 2026 the source was restored to an ignored assessment copy: its build and 33 legacy tests passed; its formatting check failed in 37 files. Useful React/asset work was reused; the target foundation now uses C# and PostgreSQL. See the architecture document for the assessment and HARNESS.md for evidence. Legacy passes do not establish target-stack acceptance.

This supersedes the old restriction that only the Juniper logo could be reused. A file's presence, an old screenshot or a previous test report does not establish that it meets this plan.

## Business scenario and working scope

The employer takes the **project manager** role without being assigned a fictional name. At Dexter's request, the introduction explains the task: “You're the project manager, getting Juniper Studio's signed website redesign ready for kickoff. Review Maya's replies, resolve the extra Shop page, then approve the brief and publish a simulated handoff.” **Maya Ellis** is the client contact at fictional **Juniper Studio**. Project **OF-1042** is a signed redesign covering Home, About, Services, Projects and Contact. The project manager needs approved goals/audience, page confirmation and sample brand-asset references. Maya's initial reply includes Shop, which is outside the agreement.

On 19 October 2026 Dexter approved **one main walkthrough with optional “Try failure recovery.”** Entry opens directly on OF-1042 without equal project-choice tabs. Below the main walkthrough, the optional action opens the separate OF-1043 recovery fixture and explains its purpose. **Back to main walkthrough** restores the saved main project. Refresh opens the main walkthrough without resetting either fixture. Two projects remain internal to the demo's isolation/recovery model; they are not two competing business walkthroughs. No API, schema, attempt-budget or business-rule change is required.

The core application contains:

- One fictional client, two projects, one service and one fixed information-request template.
- Start onboarding; prepare its record, client request, staff review task and fictional welcome message in the Outbox.
- Review each item, request a correction with a comment, approve a revised reply and mark the project ready.
- An approved kickoff brief, an inspectable simulated register handoff, Outbox and Activity.
- A private visitor sandbox, sample-reply controls, reset, repeat protection and one recoverable welcome-message failure.
- API/database and browser checks covering the acceptance criteria below.

Dexter approved the four version-one product boundaries below on 19 October 2026. The functional scope and technical approach are agreed. The working data/state design completes CP-1; detailed layout and endpoint/schema choices can be resolved during the relevant implementation slice. The main path comes first; repeat and recovery exercises are optional for visitors but required behaviours to verify.

### Version-one release boundary: decided

On 19 October 2026 Dexter chose **the working application with a clearly labelled simulated handoff for version one; the real Make-to-Google-Sheets connection comes later**. This supersedes the earlier requirement for connected proof before the first release and keeps the first version focused on application engineering.

The React interface, C# API, PostgreSQL persistence, review rules and recovery behaviour must genuinely work. Only external email delivery and the external register connection are simulated. The simulated handoff must have an inspectable result stored by the application; a success animation alone is insufficient.

Keep the real integration as a later milestone outside version-one acceptance. It will be owner-run, bounded, fictional and supported by matching execution/destination evidence. Public visitors cannot launch real provider writes. A provider failure must not disable the public walkthrough.

### Outside the core and backlog

No real customers, personal data, email sending, CRM, payments, e-signatures, uploads, additional clients/templates, general workflow builder or production business use. Do not carry old multi-client selectors, reminders or extra failure variants into the new scope automatically.

AI-assisted brief drafting and broader scenarios remain backlog ideas. They have no implementation commitment. AI must never approve answers, change scope or decide readiness.

### Employer UI pass: implemented for review

Dexter reported on 19 October 2026 that the screen is crowded and some text is too small, then authorised the planned pass. The implementation shortens role/context copy, collapses project metadata and optional history/repeat detail, uses 16px body/action text and at least 14px supporting text, and offers actions at the stage where they are useful. Mark ready appears only after approval; the relevant scope blocker, simulation label and sandbox/reset consequence stay visible. The [walkthrough](docs/employer-walkthrough.md) records the screen and review criteria. This adds no dependencies, business features or design system. Stop for local review; human timing is still required before claiming AC-10.

For a proposed addition, record the user benefit, added work and any cost in this section. Dexter must agree to promote it into scope; an agent must not update the plan merely to justify work it already started. Prefer removing or replacing scope when adding work.

## Business rules and acceptance

These IDs are the shared contract for implementation and the harness. Local checks cover the main journey, reliability and the revised employer UI, including readable text, contextual actions, keyboard/phone interaction and reaching review in two actions. HARNESS.md records exact evidence and limits. Automated checks do not establish first-time human understanding/timing or hosting acceptance; AC-10 remains open.

| ID | Required outcome | Verification to retain |
| --- | --- | --- |
| AC-01 | Starting a project produces one Onboarding, request, review task and welcome message. Repeats reuse them. OF-1043 remains valid for the same client. | API/database checks for repeated and overlapping starts, plus an inspectable result. Uniqueness is within the visitor sandbox and Project. |
| AC-02 | Shop cannot be approved against the signed five-page scope. The API enforces the same rule as the screen. | Invalid-approval API check and browser feedback. |
| AC-03 | Requesting changes requires a comment. Preserve submitted revisions and their review decisions; a corrected reply requires fresh approval. An approval belongs to the current revision. | API checks for a missing comment, stale/conflicting decisions and invalid approval reuse; browser correction flow and Activity. |
| AC-04 | Mark ready requires successful setup and approval of every required item's current revision. Store the approved brief, close the staff review task and freeze replies/reviews for that project. | API readiness checks, repeated Mark ready returning the same brief and the complete browser journey. No reopening workflow in version one. |
| AC-05 | The welcome-message failure keeps completed work. Allow three attempts total per project, including the first; only an explicit retry consumes another attempt. Repeats and restoring service do not reset the budget. After three failures preserve work and require sandbox Reset for a fresh budget. | Check failed attempt one, recovery on attempt two, exhausted budget, replayed/overlapping retries and independent project budgets. Compare retained output identities and counts. |
| AC-06 | Readiness and simulated handoff confirmation are separate. Repeated publication reuses its sandbox register record. Failure or uncertain confirmation never becomes false success. | State, repeat and lost-confirmation checks against the application-owned simulated register. Real Make/Sheets proof is outside version-one acceptance. |
| AC-07 | Visitor sessions cannot read or mutate each other's records. The sandbox expires 24 hours after creation; activity never extends it. Refresh preserves active work. Reset replaces both sample projects for only that visitor. Expired/reset credentials cannot access old work. | Two-session API/browser checks for isolation, refresh, reset and the exact expiry boundary. A fresh start must not replay an old mutation; access expiry does not depend on physical cleanup timing. |
| AC-08 | Loading, empty, waiting, blocked, failure and success states have clear text and useful next actions. The journey works by keyboard and on a phone. | Browser checks and visual/keyboard review. |
| AC-09 | Sample replies and simulated messages/handoffs are labelled. Visitors cannot trigger real provider writes or access credentials. Activity identifies actor, action, outcome and time, including demo actions. | Boundary checks and a review of visible labels and Activity. |
| AC-10 | A first-time visitor understands their role, reaches a useful result within two actions and completes the main path in under five minutes without coaching. A visit after inactivity must not impose an approximately minute-long startup wait. | Timed fresh/post-inactivity entry and a human walkthrough. About four minutes is a design target, not a measured result. |
| AC-11 | Frontend actions genuinely use the C# API and PostgreSQL. The repeatable checks cover actual business outcomes. | Target-stack build, API/database and browser evidence; a planning mockup or old D1 test is insufficient. |

Version-one publication also requires verified zero-additional-cost hosting/account fit, local review, a short truthful case study, source and demonstration media, and Dexter's deployment instruction. The real Make/Sheets integration is not a first-release gate. Do not claim measured time savings, production reliability or live integration from simulated evidence.

The 19 October 2026 final local review followed the main path and optional recovery on desktop and phone, including the explicit expired-session restart. The current scope is coherent; no additional feature or architecture milestone is required. AC-10 still needs first-time human understanding/timing and public startup evidence. The release package is preparation for a later deployment decision: its case study/script are drafts, and its source commit is local. It does not establish hosted acceptance or authorise a push, release publication or deployment. Do not turn optional polish into another redesign or planning round.

## Agreed version-one boundaries

On 19 October 2026 Dexter accepted all four recommendations. These are now version-one scope boundaries, alongside the integration decision above.

| Decision | Agreed version-one boundary | Scope consequence |
| --- | --- | --- |
| Project entry and setup | Open directly on the prepared signed Juniper Studio main walkthrough. Keep the second fixture behind optional Try failure recovery, with an explicit return to saved main work. One client, two stored projects, one service and one request template remain. | Project/client creation and editing workflows are outside version one. |
| Staff and client interaction | The visitor stays in the project manager role, without a fictional visitor name. Labelled demo controls supply Maya's original and corrected replies. | No separate client portal, client accounts or real invitation delivery in version one. Each actual review decision belongs to the project manager. |
| Out-of-scope requests | Flag Shop, request a correction with a comment, then review the revised page list. | No quote, price change, contract variation or route for approving additional scope in version one. |
| Reliability depth | Verify repeated Start/Publish and one independent welcome-message failure/recovery scenario on OF-1043. Dexter confirmed three attempts total, including the first, on 19 October 2026. | Visitors may skip the exercises. Restoring service does not reset the limit. Additional failure variants, reminders and a general automation engine are outside version one. |

Review the actual main screen against these boundaries during its implementation slice. Target behaviours such as isolation, honest simulation labels, approved-only briefs and server-enforced rules are already acceptance requirements, not optional scope reductions.

## CP-1: stop planning and move to a bounded build

On 19 October 2026 Dexter requested a sharp end to planning. **This data/state pass is the last scheduled planning pass.** CP-1 is ready when the five documents consistently define scope, the main journey, the data/state rules, acceptance and the first implementation boundary. This checkpoint assesses specification completeness, not working software.

Dexter authorised the **baseline and foundation**, **main vertical slice**, **reliability and simulated handoff**, **employer UI improvements/demo-readiness checks**, final local review, **source/release-package preparation**, **hosting validation** and then **alwaysdata Free setup/hosted acceptance** on 19 October 2026. Complete this bounded hosted check and stop before employer distribution. Do not insert another general planning round. Human timing, hosted acceptance, actual recording and source publication remain separate evidence gates.

Reopen a settled decision only for a material contradiction, a failed feasibility check that prevents an acceptance requirement, or Dexter's scope change. State the specific blocker and smallest decision needed; do not restart discovery or add planning documents. New ideas stay in the backlog and do not delay the agreed milestone.

| Topic | Checkpoint outcome and next permitted work |
| --- | --- |
| Existing-code assessment | Original tree preserved; isolated build/tests assessed and reuse decisions applied. The legacy formatting failure is recorded separately. |
| Application and behaviour | Technical approach accepted; working data model, revision-bound approvals, three-total-attempt rule, transactions and fixed 24-hour sandbox specified in the architecture document. Verify them in implementation. |
| Implementation checkpoints | Working app/local review and source/setup/presentation-draft package complete. The later hosting reassessment supersedes proceeding with alwaysdata activation. Human timing, recording and hosted acceptance remain open. Source publication and employer distribution are not authorised. |
| Public hosting feasibility | The authorised Deplexo Free app and Neon Free PostgreSQL 17 now pass secure workspace creation, the real workflow and bounded recovery/security checks. The verified Cloudflare scheme adapter resolves Deplexo's incorrect standard forwarding header without weakening cookies or proxy trust. Normal suspended-database entry is measured; hour-idle/overnight entry, human timing and employer-access reputation still need evidence. Alwaysdata remains unsubmitted; no further signup is needed for this slice. See the architecture and harness. |

Record material decisions here with an absolute date, reason and affected scope. Keep milestones and evidence in [HARNESS.md](HARNESS.md), not a second roadmap. On 19 October 2026 Dexter rejected credit-card verification and requested an open source alternative using OpenAlternative. Northflank setup was stopped; no payment method or app/database/job was created. His later instruction authorises alwaysdata Free setup and hosted acceptance, with review before employer distribution. Preserve the existing React/C#/PostgreSQL app and reviewed frontend; do not rewrite it or install a hosting dashboard to justify a provider choice.

Only ongoing free plans without card verification qualify: no temporary-credit dependency, purchased server, paid quota increases or automatic paid fallback. Verify the whole app/database arrangement, not software licensing alone. Preserve work and stop the affected connected action when an allowance is exhausted. Deplexo Free + Neon Free is the authorised route; the remaining extended-idle and release gates still apply.
