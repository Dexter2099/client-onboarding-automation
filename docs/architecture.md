# Architecture and existing-code assessment

Updated 19 October 2026. **Version-one source/review package prepared; stop for review.** The accepted architecture is unchanged. The local source commit and setup/presentation drafts are preparation for deployment; human timing, hosting validation, actual recording and public publication/deployment remain open in [HARNESS.md](../HARNESS.md). No deployment is authorised.

## Existing baseline

The initial application needs improvement; this is work on an existing project. Its technical quality and reuse suitability must be assessed against the new requirements rather than inferred from that description.

On 19 October 2026 the working directory contained planning files and no checked-out application source. Local `main` had no commit, the connected GitHub repository returned no branches, and the old plan's `v1-archive` reference could not be verified. A recoverable local Git tree containing 67 files was located and preserved:

- Tree: `01505e94b0c309ba8138dffe8bb4ce7846ab1d54`.
- Local tag: `codex/legacy-prototype-2026-10-19`.
- The tag points to a tree, not a commit or runnable branch. It has not been pushed. It protects the snapshot from being left unreferenced locally.

Read-only inspection found React/TypeScript screens, a Cloudflare Worker, D1 SQL migrations, Vitest tests and verification configuration. No source was restored, dependencies installed, application run or legacy test executed during this pass. Historical reports inside the snapshot remain unverified evidence.

## Initial observations and reuse questions

Paths below are inside the preserved snapshot, not the current working tree.

| Area inspected | Observation | Proposed disposition; runtime validation remains pending |
| --- | --- | --- |
| `src/shared/domain.ts` | Three clients/services and multiple scenarios; Alex appears as Juniper's client contact. | Repair/narrow the fixtures and domain terminology to Juniper, two Projects, Alex as staff and Maya as the client. |
| `src/web/App.tsx`, styles and brand assets | Existing React UI and guided/reliability interactions. | Retain compatible visual primitives, assets and styles where useful; repair the workflow around the approved project workspace. Reuse is provisional until the screen and accessibility are checked. Preserve relevant third-party notices. |
| `src/worker/engine.ts` | Worker/D1 job processing with retry limits, leases and stored effects. | Replace runtime-specific backend code with C# implementation of the required behaviours. Carry forward useful failure/repeat cases; do not port the whole job engine automatically. |
| `src/worker/index.ts` and D1 migrations | The old API fingerprints client plus service; the SQL uses session-owned runs and stored jobs/effects. | Replace those API/storage assumptions with sandbox-owned Projects and Onboardings. A second Project for the same client/service must be valid. PostgreSQL needs new reviewed migrations, not a mechanical D1 copy. |
| `src/web/guided.ts`, `tests/guided.test.ts` and backend tests | The old guide selects further client/service combinations and gates reliability exercises on completion. Its duplicate helper compares counts. | Repair useful scenarios; retire obsolete selection/gating expectations. Verify stable identities as well as counts, and keep OF-1043 independently accessible. Old tests must not dictate the new product. |
| `package.json`, lockfile and configuration | npm scripts for development, build, tests and formatting exist in the snapshot. | Retain npm for reused frontend work; review versions and replace Worker/D1-specific commands. Nothing here is verified runnable for the target stack. |

These initial recommendations came from source inspection. The runtime pass below records what was actually verified and reused. Preserve the original snapshot. No real customer-data migration is required by the agreed fictional-demo scope.

## Runtime assessment and foundation outcome

After Dexter's implementation instruction on 19 October 2026, the full snapshot was restored to ignored `.local/legacy-baseline`, without overwriting the five documents. `npm ci` succeeded, `npm run verify` built the React app and passed 33 tests across two files, and `npm run format:check` failed in 37 files. The latter is a pre-existing baseline failure, not a target-stack test failure. No legacy deployment script was run.

| Decision | Applied outcome |
| --- | --- |
| Retain | npm/lockfile approach, compatible React/Vite/TypeScript dependencies, Inter, Lucide, visual tokens, brand mark, Juniper image, skip-link/entry-point structure and notices. |
| Repair | Basic button and API-helper patterns, responsive styling and project screen were narrowed to two signed Juniper projects, correct staff/client roles and scope-accurate preview copy. The old polling/guided-run screen was not carried across. |
| Replace | Worker/D1 API/storage replaced with one ASP.NET Core application and EF Core/Npgsql/PostgreSQL. The foundation has no job engine, repository layer or background worker. |
| Retire from current source | Extra clients/services, enquiry creation, old guided gates, automatic retry/reminder flows, provider/deployment scripts and legacy-only tooling. Their original source remains in the preserved tree. Useful negative/recovery cases can inform later tests within the approved scope. |

The foundation introduced **SandboxSessions and Projects**, plus EF's migration-history table. Creation seeds OF-1042 and OF-1043; project data is read-only. The server stores only a random token's SHA-256 hash, enforces ownership and fixed expiry, and atomically replaces the owned session/projects on Reset. PostgreSQL enforces the 24-hour lifetime, foreign key/cascade and per-sandbox project-reference uniqueness. The composite project index also covers its ownership foreign key. The main slice adds the workflow data described next.

The React build is served by the C# application; Vite proxies `/api` during frontend development. POSTs require ASP.NET Core antiforgery protection. Cookies are HttpOnly/SameSite Strict and Secure outside loopback HTTP development; API results use `no-store`. Expiry is checked on the server; the interface offers an explicit fresh start and never replays an expired workflow mutation. The new workflow endpoints use those same ownership/expiry boundaries.

Verified versions: Node 24.19.0, npm 11.17.0, .NET SDK 10.0.402/runtime 10.0.13, EF Core/EF tools 10.0.13, Npgsql EF provider 10.0.3 and PostgreSQL 17.11. The machine's global .NET SDK was 9.0.300; a SHA-512-verified official .NET 10 archive was installed under ignored `.local/dotnet`, without replacing the global SDK. Keep EF Relational at the same patch as EF Design; the initial mixed transitive patches caused an assembly warning that was corrected before verification.

The foundation originally passed twelve API/database and three browser tests; the main slice increased these to 21 and five. Current reliability checks and their exact results are in HARNESS.md. The same target stack and package versions are retained. Live hosting, connected integration, first-time human timing and CI execution remain unverified.

## Main vertical slice: implemented

The additive `MainWorkflow` migration added Onboarding, RequestItem, ItemRevision, OutboxMessage and ActivityEvent. The subsequent `Reliability` migration brings the total to nine business tables with WelcomeAttempt and RegisterEntry. Required information and the approved brief use typed application payloads stored in JSONB; review decisions and metadata stay attached to immutable submitted revisions. The fixed request/task identities and closure time live on Onboarding. Activity uses a per-Onboarding sequence for stable chronological display, including when test timestamps match.

| Route | Current action |
| --- | --- |
| GET `/api/projects/{id}/onboarding` | Owned workflow state, item revisions, simulated Outbox, Activity and stored brief, or Not started. |
| POST `/api/projects/{id}/start` | Create fixed setup, then attempt the fictional welcome in a separate transaction. Repeated Start reuses setup and never retries. |
| POST `/api/projects/{id}/reply` | Supply a labelled initial or corrected sample. A corrected sample targets the reviewed Pages revision; Goals/Assets remain unchanged. |
| POST `/api/projects/{id}/review` | Approve the current revision or request the prepared Pages correction with a required comment. Actor comes from the server-owned project, not the request body. |
| POST `/api/projects/{id}/ready` | Require current approvals, save the brief snapshot, close the review task and freeze further replies/reviews. |
| POST `/api/projects/{id}/welcome` | Explicit attempt with a non-empty UUID command ID, expected version and prior attempt count. Exact replays reuse results; changed inputs on a used command conflict. |
| POST `/api/projects/{id}/restore` | Restore the owned simulated welcome-service flag, without retrying or changing the budget. |
| POST `/api/projects/{id}/publish` | Require a saved ready brief; persist one frozen simulated register entry and confirmation. Repeats reuse it. |

The implementation stays in the existing C# app with direct EF Core access and one React workflow panel; no new dependency, service, queue or workflow framework was introduced. Each mutation locks the session row used by Reset, checks the owned project, validates the expected Onboarding version/current revision and rechecks expiry before commit. PostgreSQL constraints protect one onboarding per project, required-item kinds, revision/submission identity, correction comments, message keys and ready/snapshot consistency. Foreign-key ownership paths are indexed and cascade on sandbox reset.

Sample replies come from fixed server-owned fixtures and do not approve themselves. The original Pages contains Shop and is accepted into review; approval is rejected until the five signed pages are confirmed. The corrected Pages is a fresh revision, preserves the earlier comment/decision and retains unchanged Goals/Assets approvals. Review outcomes are terminal per revision. The saved brief includes approved revision identities/reviewers and approved content, and is read from that snapshot on refresh.

Welcome preparation and correction messages are saved in the labelled Outbox; Activity records actor, action, outcome and time with the business change. No message is delivered. New OF-1043 onboardings start with simulated welcome availability false; OF-1042 starts available. Restore and explicit Try welcome recover the former on attempt two. A maximum of three committed rows is enforced by the attempt number constraint and unique onboarding/number and onboarding/command indexes. Expected version/count and the session lock prevent distinct overlapping commands spending twice from one observed state.

The additive `Reliability` migration backfills every already-prepared welcome as successful attempt one, retaining its preparation time and existing output identities. Previously successful OF-1043 work remains successful; it is not retroactively failed. Pending OF-1043 work receives the unavailable flag. There is no reset or fresh budget during upgrade.

RegisterEntry has a unique Onboarding and stable `SIM-` reference, a JSONB copy of the approved brief and a confirmation time. Entry, confirmation and Activity commit together; readiness remains separately stored. Unknown welcome/publication responses require a read before another UI mutation, so a committed result is discovered without optimistic confirmation or a blind new attempt. Fault-injection tests use short-lived triggers only inside disposable test databases; no failure-injection subsystem exists in the app.

## Employer UI pass: implemented

The existing React App/OnboardingPanel and CSS now shorten the introduction and stage instructions, collapse project metadata with native details, and show sample/review/ready/handoff actions when relevant. Exact signed pages stay beside Pages review. Body/action text is 16px, supporting text at least 14px, and the main controls have a 44px minimum height. Existing tabs/details retain Outbox, Activity, earlier revisions and approval/register evidence.

Simple heading refs preserve orientation when replies arrive, answers change or readiness opens the brief. Error navigation keeps the Load saved state action visible until an uncertain response is resolved. The handoff control precedes the longer brief content. This slice changes no API, database schema, dependency or business rule; the nine-table model and same-origin architecture remain unchanged. Browser and visual evidence are in the harness. First-time human timing and public hosting acceptance are still pending.

## Intended target boundaries

| Part | Responsibility |
| --- | --- |
| React + TypeScript | Display project state, capture review actions, provide validation feedback and accessible responsive interactions. |
| One ASP.NET Core application in C# | Validate requests, enforce session ownership and business rules, coordinate state changes, expose the API and record outcomes. |
| PostgreSQL | Persist relationships, approved/submitted information, constraints, status and Activity. Protect repeat safety at the data boundary as well as in the UI. |
| External-service boundary | Labelled simulated email/register behaviour for version one, with its result persisted by the application. A controlled Make/Sheets connection belongs to a later milestone and is not a first-release dependency. |

Keep one frontend, one backend and one relational database. The selected direction does not require microservices, a generic workflow engine, a message broker, event sourcing, multiple backends or an agent framework inside the product. Add infrastructure only when a concrete requirement cannot be met simply.

Use the business terms Project, Onboarding, Request item, Step, Outbox and Activity. Model client replies/revisions, review decisions and handoff attempts only to the depth needed for the acceptance criteria. An anonymous sandbox session still needs server-enforced ownership; a visitor ID supplied in a request is not sufficient authorisation.

Committed work must survive a failed step or browser refresh. Repeated or overlapping requests must not duplicate their business outputs. Handoff status is independent of readiness; uncertain provider confirmation requires checking the destination identity before another creation. Keep failure injection limited to the owned fictional sandbox.

## Agreed application shape

On 19 October 2026 Dexter accepted and authorised this technical approach through the reliability/simulated-handoff slice. Verified patches are above:

- Use one ASP.NET Core application to serve the built React files and `/api` from the same origin in production. Keep Vite for frontend development with an API proxy. This keeps cookie/session handling and deployment small; it does not require separate frontend hosting.
- Target **.NET 10 LTS**, with compatible Entity Framework Core and **Npgsql** for PostgreSQL. Choose exact package patches and a supported PostgreSQL major during foundation verification. Microsoft lists .NET 10 as supported through November 2028; Npgsql documents its EF Core provider and 10.0 release.
- Keep one C# application project with small endpoint/business/data responsibilities and a test project. Avoid extra architectural layers or a generic repository/workflow framework unless an actual requirement warrants them.
- Use ordinary PostgreSQL transactions, foreign keys, unique constraints and explicit revision checks. Store the fictional Outbox and register result in this database. The application process and its local disk are not authoritative storage.

## Data model for checkpoint CP-1

All nine records below are implemented within the agreed scope. Use opaque internal IDs and UTC timestamps. Public references such as OF-1042 are unique within a sandbox, not authorisation credentials.

| Record | Main data and relationships | Integrity rule |
| --- | --- | --- |
| SandboxSession | Token hash, creation and expiry; owns its two Projects. | Unique token hash; expiry is creation plus exactly 24 hours. Store no raw session token. |
| Project | Sandbox owner, public reference, fictional client/contact, Alex, signed pages and service. | Unique sandbox/reference. Prepared immutable fixture data; OF-1042 and OF-1043 are distinct even with the same client/service. |
| Onboarding | Project, fixed request/review-task identities, concurrency version, welcome-prepared/ready timestamps, approved brief snapshot and simulated welcome availability. | At most one per Project. The fixed review task closes when marked ready; no general task table is needed. |
| RequestItem | Onboarding and kind: Goals, Pages or Assets. | One of each kind per Onboarding. Its latest revision defines current review state. |
| ItemRevision | Item, revision number, stable sample-submission key, typed bounded payload, review decision/comment, actor and timestamps. | Unique item/revision number and item/submission key. Content is immutable; review belongs to that exact revision. |
| WelcomeAttempt | Onboarding, attempt number, UUID command key, expected version/count, committed prepared/failed outcome and timestamp. Activity holds safe error detail. | Attempt number is 1, 2 or 3; unique onboarding/number and onboarding/command key. This small ledger prevents retries of one request consuming a second attempt. |
| OutboxMessage | Onboarding, non-null message key, fictional recipient and prepared content. | Unique onboarding/message key: `welcome` or `correction:<revision-id>`. It records preparation, never real delivery. |
| RegisterEntry | Onboarding, stable register reference, frozen brief content and confirmation time. | One per Onboarding, created only after readiness. Repeated Publish returns the same entry. |
| ActivityEvent | Onboarding, sequence, actor, action, outcome, time and bounded details. Ownership follows Onboarding -> Project -> SandboxSession. | Write with the business change. This slice needs project history only, not standalone session events. No token/credentials; this is history, not the source of business state. |

Client, staff owner and service are prepared Project data; there are no account-management, CRM, generic job or workflow-definition tables. The brief is a snapshot on Onboarding, including the three approved revision IDs and approved goals/pages/asset references/owner. It needs no separate document subsystem. WelcomeAttempt is separate from display history so its three rows enforce the command/attempt budget.

Foreign keys preserve each ownership chain. Unique constraints prevent duplicate outputs; local checks bound attempt numbers and require review metadata/comment where applicable. Cross-record rules such as all-current-items-approved are rechecked by transactional C# operations; they cannot be expressed as a simple cross-table PostgreSQL CHECK.

## State transitions and approvals

Keep session validity, setup, item review, readiness and publication separate. Derive the overall screen status rather than persisting another competing project-status machine. All transitions below are implemented locally.

| Concern | Allowed progression | Guard or result |
| --- | --- | --- |
| Setup | No Onboarding -> fixed setup created -> welcome Pending -> Prepared | Start creates the Onboarding, request identity, review-task identity and three empty items once. Successful welcome preparation completes setup. |
| Setup failure | Pending -> Failed, then explicit retry -> Prepared or Failed; the third failure -> Exhausted | Existing setup work stays intact. Repeated Start does not retry welcome or consume an attempt. |
| Item review | Awaiting reply -> Awaiting review -> Approved or Changes requested | Approve/Request changes acts on the latest submitted revision only. Changes requested requires a non-blank comment. |
| Correction | Changes requested -> new revision Awaiting review -> Approved or Changes requested | Preserve the previous reply/decision. A new revision never inherits approval. Other unchanged items retain theirs. |
| Readiness | Not ready -> Eligible, then explicit Mark ready -> Ready for kickoff | Eligibility is derived: setup complete and the latest revision of each required item approved. Eligibility alone does not mark ready. |
| Publication | No register entry -> confirmed RegisterEntry after Publish | Requires Ready. Entry, confirmation and Activity commit together; the Project remains Ready even if a publication request fails. |
| Sandbox | Active -> Expired at 24 hours; explicit Reset replaces Active with a new sandbox | Expiry is checked server-side; no action automatically extends it or carries old data into a new sandbox. |

The main path supplies one initial sample reply containing all three items; the corrected sample changes Pages only after its correction request. Stable submission keys make repeated sample controls reuse their revisions. Shape-valid Pages containing Shop are accepted for review but flagged as outside the signed scope; approval requires exactly the five agreed pages. Rejecting that submission before review would hide the intended business problem.

Review outcomes are final for that revision. An identical repeated decision returns the saved result without another message or Activity event; a conflicting or stale decision is rejected and the screen reloads current state. Request changes saves its comment, review decision, correction Outbox message and Activity atomically.

Mark ready checks all current approved revision IDs within its transaction, creates the brief snapshot and closes the fixed review task. Repeating it returns that same snapshot and timestamp. Reply/review changes are frozen after Ready in v1; only reads, publication and sandbox Reset remain relevant. There is no reopening or editing workflow.

For the page header: incomplete/failed setup takes priority; complete setup without replies is Waiting for client; a correction request is Waiting for correction; any pending review is Needs review; all approved but unmarked is Review complete; a saved brief is Ready for kickoff. Handoff confirmation is shown separately. Loading, in-flight requests and uncertain HTTP responses are presentation states, not durable business statuses.

## Transactions, replays and concurrency

The implemented version-one API actions are session start/load/reset, project reads, Start, sample reply submission, revision review/correction, Mark ready/brief read, Publish, Restore simulated service and Try welcome. Brief and register reads are part of the owned workflow view. There are no client/project administration endpoints or generic CRUD surface.

Use short transactions with consistent lock order: SandboxSession, then the affected Project/Onboarding and child records. Serialise writes within a sandbox so Reset, expiry checks and business actions cannot race. Recheck active ownership after acquiring locks and at the commit boundary; roll back if the sandbox has expired. Include an expected Onboarding version/current revision in new mutations; reject stale actions without silently overwriting them.

After active-session authorisation, recognise an exact saved replay before rejecting its stale version. Replays return saved identities/current state and create no new effects. Different payloads using the same command/submission identity conflict. A timeout is not permission to generate a new operation key and blindly repeat a mutation.

Commit fixed setup before attempting welcome, so message failure cannot erase the request or task. If processing stops between those stages, the snapshot shows incomplete setup; the explicit welcome action can continue it. Each welcome result, attempt ledger row and any welcome message commit together. A database rollback creates no message and no committed attempt. If commit succeeded but the response was lost, reuse the command key or reload to find the saved result.

Publish similarly creates its register row and confirmation in one PostgreSQL transaction. A failed transaction leaves no entry; an uncertain HTTP response triggers a read, not a false success or a second record. No durable publishing queue, callback service or distributed reconciliation is needed for the v1 simulation.

## Welcome retry limit: three total

On 19 October 2026 Dexter chose **three attempts total per Project's Onboarding**, including its initial attempt. Restoring the simulated service does not reset or increase that budget.

- Each explicit Try welcome command can consume one attempt. There is no timed or automatic retry loop. Initial Start makes the first attempt only for a newly created Onboarding.
- Replaying a command uses its saved WelcomeAttempt. Parallel retries based on the same previous attempt count cannot both consume budget: the second new command conflicts and must reload.
- Once a welcome message is prepared, later retries return it and consume no attempts. Restore changes only the owned simulated-service flag and is itself repeat-safe.
- After three committed failures, preserve setup/history and show Exhausted. Retry is unavailable even after Restore. Reset starts a fresh sandbox and is the only way to obtain a fresh budget in v1; there is no per-project counter-reset endpoint.
- The guided recovery is failure on attempt 1, Restore, success on attempt 2. A separate negative check exhausts all three without restoring. Both Projects have independent budgets.

## The fixed 24-hour sandbox

Use a server-issued opaque token in a Secure, HttpOnly, same-site cookie with same-origin/CSRF protection for changes. The expiry is fixed at creation plus 24 hours, not a sliding inactivity timer. Refreshes, reviews, retries and publications never extend it. The browser's countdown is informative; the server clock decides access, including at the exact expiry boundary.

While Active, refresh retains committed state. At expiry, reads/mutations are denied and the UI offers an explicit fresh start. Do not automatically create a new sandbox and replay a stale action. Starting again creates new internal IDs and clean copies of both Projects.

Reset atomically invalidates/deletes only the current sandbox and its owned data, then creates a fresh sandbox/cookie and fixtures. It resets both sample Projects, not just the open one. Other visitors are unaffected. Old-cookie requests are rejected; the frontend clears its cache and ignores pre-reset responses rather than displaying them in the new session.

Access ends immediately at expiry; physical deletion can happen later through bounded cleanup during fresh-start session creation. The foundation deletes at most 20 expired sessions per such request. This is a 24-hour access lifetime, not a promise that inactive-host rows are deleted at that exact instant. Index expiry and ownership paths, cap cleanup batches and keep all fixture data within measured free-tier limits. No always-running cleanup scheduler is required for v1. Failed cleanup must not restore access or delete an active sandbox.

## Hosting feasibility checked on 19 October 2026

No provider account was created, changed or verified, and no deployment was attempted. These are current published-plan findings, not account-specific acceptance:

| Candidate | Finding and planning status |
| --- | --- |
| Northflank Sandbox | First candidate to assess: its pricing advertises always-on free compute, two services and one database. Its database documentation includes PostgreSQL. A single application container plus PostgreSQL matches the proposed shape on paper. Verify actual free resource/storage/egress/build entitlements and account eligibility, then measure the app; no zero-cost or latency guarantee has been established. |
| Render Free | Excluded: its documented idle behaviour can produce an approximately one-minute wake-up, which Dexter rejected. Its free PostgreSQL also expires after 30 days and does not meet the ongoing-free requirement. |
| Neon Free | A database-only alternative if needed, not an additional required service. Published limits include 1 GB/project, 100 CU-hours/project/month and scale-to-zero after inactivity. Usage caps can suspend compute. Account fit and end-to-end wake-up performance would need verification. |

Prefer the simple application/database arrangement if it meets the cost and startup requirements. Do not hide backend unavailability behind an immediately rendered static shell, add paid fallback, or change the selected backend to fit a provider. The older Workers/D1 assumptions are historical.

## Planning checkpoint and execution boundary

This data/state design completed checkpoint CP-1 in [HARNESS.md](../HARNESS.md). Foundation, the main slice, reliability/simulated handoff and employer UI improvements are implemented without another general planning round. Stop for review. The application shape, fixed 24-hour lifetime and three-attempt limit remain unchanged.

The implemented migrations, routes and evidence are recorded above and in the harness. Human walkthrough timing remains an evidence check; hosting account/resource/performance validation remains publication work, and hosting is unselected. The final desktop/phone review found no need for another architecture or feature milestone. No deployment is authorised. Surface a material conflict rather than silently broadening scope.

Sources: [ASP.NET Core static files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0), [.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [Npgsql EF Core provider](https://www.npgsql.org/efcore/), [Npgsql 10.0](https://www.npgsql.org/efcore/release-notes/10.0.html), [Northflank pricing](https://northflank.com/pricing), [Northflank databases](https://northflank.com/docs/v1/application/databases-and-persistence/deploy-a-database), [Render Free](https://render.com/docs/free), [Neon plans](https://neon.com/docs/introduction/plans).

Data-integrity references: [PostgreSQL constraints](https://www.postgresql.org/docs/current/ddl-constraints.html), [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
