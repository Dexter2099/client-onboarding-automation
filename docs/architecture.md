# Architecture and existing-code assessment

## Production packaging and rollout — 25 October 2026

The normal npm/Vite build now compiles the reviewed interface in preview/content-snare into the ASP.NET static root; the isolated preview build remains available. The multi-stage Docker build copies that UI and its shared field/source specification, self-hosts Montserrat/Open Sans with their licences, and includes a silent H.264 MP4 plus the preserved original WebM recording. CSP adds same-origin media while retaining self-only script/style/font/connect rules. The recording stays labelled as local Ollama footage.

Production intake requires Intake:Enabled=true, OpenAI, the verified dedicated project ID, server-side credentials and budget verification; wrong projects/providers fail startup. Default Production remains off for safe rollback. The request endpoints inherit owned sandboxes, CSRF and the existing rate limits. The pinned image uses workstation GC and a 64 MiB managed heap ceiling to leave native-process headroom within Deplexo's 128 MiB allocation; this is a managed-heap ceiling, not a process-memory guarantee. See [Microsoft's runtime configuration](https://learn.microsoft.com/dotnet/core/runtime-config/garbage-collector).

CI runs deterministic backend tests and the packaged UI in Chromium/Firefox/WebKit against a real local Kestrel API and disposable PostgreSQL. A model double exists only in the test assembly; the production binary has no test provider. Explicit live evaluations remain separate. After deployment, HTTPS API checks verify revision/security/storage/media; native macOS WebKit performs one real hosted-model visitor journey within the same shared spending allowance.

## Hosted provider adapter — 25 October 2026

OpenAiIntakeClient implements IIntakeModelClient with direct server-side Responses HTTP and the pinned gpt-5.4-2026-03-05 snapshot. It converts the existing two read tools/history, requests strict structured output, counts the actual input/schema, reserves maximum cost and then generates with Standard processing, no storage, 512 output tokens and a 15-second overall deadline. No SDK, new host or service. Explicit Intake:Provider also retains Ollama and the failed Cloudflare comparison adapters; there is no automatic switch. Only OpenAI is the selected hosted path.

IntakeSpendBudget owns one independent PostgreSQL reservation table, keyed by OpenAI project/month in UTC. Short advisory-lock transactions make reserve/settle atomic across instances; no lock spans inference. US$4.50 is the application ceiling below the verified US$5 provider hard cap. Unknown outcomes retain their full hold. Settlement counts full ordinary input pricing and output pricing, conservatively ignoring cache discounts; duplicate settlement cannot refund again and a usage overrun stops further calls for review. Sandbox reset/expiry cannot erase spend. A separately scoped EF context keeps budget transactions independent of request state. Live model evaluations use the persistent loopback development ledger even though their request databases are disposable. Release must carry the current project/month ledger into the shared hosted database before inference is enabled.

The adapter bounds input at 8,192 counted tokens/64 KiB, reads responses within 64 KiB, disables redirects and redacts provider errors. Existing six-call and three-run reservations still count failed/uncertain attempts. Continuation refuses a changed model. Application code validates fictional identifiers against the actual cited text; missing-value statements cannot become identifiers. A valid clarification pauses when it is the only unresolved required field, regardless of the provider's done flag. Traces retain usage, timing and conservatively accounted USD without credentials or hidden reasoning. Production routes remain disabled during this checkpoint; HARNESS distinguishes real-model evidence, deterministic tests and older local baseline results.

**Local implementation status — 25 October 2026:** C#/ASP.NET Core now owns the request API, PostgreSQL aggregate, source validation, bounded native-tool loop and call allowance. The reference UI calls this API; browser-only persistence and canned fill values have been removed. Installed Ollama qwen3.8:27b supplies real inference over loopback with cloud disabled. Fictional documents/actions remain labelled. Production does not map these local intake endpoints. The older preview-only/Cloudflare-provider statements below are historical; public hosted intake remains undeployed. HARNESS owns current verification results. No live app, site, hosting, CI release or deployment change was made.

## Authorised local implementation — 25 October 2026

Dexter has now authorised the C#/.NET request backend, real local AI and connection to the reviewed reference interface. Implement A0–A3 locally using installed Ollama (no cloud account or API fees), PostgreSQL and the existing .NET app. This supersedes earlier UI-only/source-change holds. Preserve the reference layout. No live app, website, hosting, CI release or deployment change is authorised. Public hosted inference remains future work.

## Current C# reference rebuild — 25 October 2026

PLAN CS-01–CS-07 replace the four-field design. Dexter reaffirmed C#/.NET. Use React/TypeScript UI, C#/ASP.NET Core APIs/request rules/agent and PostgreSQL with EF Core/Npgsql. No Python/LangGraph or automatic switch to Blazor. The reference frontend stays in preview/content-snare/ and connects to the existing C# host through the same-origin Vite proxy.

### Current local code and future boundary

Server-owned ClientRequests/demo.json defines the fixed fields and fictional sources and is shared with the frontend. IntakeStore owns transactions/revisions/submissions, IntakeRules pure validation, IntakeAgentService the bounded native-tool loop and OllamaIntakeClient model HTTP. The UI uses api.ts and PostgreSQL state; browser localStorage and canned extraction were removed. Quotes are reconstructed from retrieved paragraphs. Only external actions remain simulated.

The local reference UI now calls the same-origin .NET API. Replacing the published main frontend remains release work. Do not force 23 bookkeeping items into Goal/Audience/Pages/Assets. Reuse ownership, fixed expiry, CSRF, error handling, database composition and delivery; old domain/UI constraints are superseded.

One small C# request service owns fixed field rules, draft revisions and submissions; bounded agent service owns model/tools/budgets/proposals. Inject a narrow HttpClient model adapter; separate pure output/source validators. Direct EF Core/existing DI suffice. No projects-per-layer, generic repository, workflow engine or interface per class.

Read-only tools read_required_fields and read_client_document bind to owned request/allowlisted source versions. Model chooses sources/one clarification/resume; no arbitrary URL/SQL/file/credentials, submission/approval, payment or messaging tools. Four manual-only fields are ineligible. App validates/saves drafts; visitor submits.

### Persistence, provenance and SOLID

Plan one owned client-request aggregate attached to existing sandbox lifetime, with expected version, bounded typed answer/provenance/status JSON and bounded saved-agent-run state. Retain sufficient revision/history for edits and source invalidation. Resolve exact migration in A1, without general form/document-builder schema. Definition is fixed code.

Store field value/author/draft-or-submitted/answer revision and document/version/section/quote. Edits remove extraction provenance and invalidate submission. Page submission is explicit application authority. Attachment is fictional reference, not real parser/file proof. Real finance/access integration excluded.

Reserve command/allowance in short transactions; infer outside locks. Persist source/schema/prompt versions, checkpoints, consumed/reserved calls and expiring claim. Replays reuse saved runs; changed-input identity conflicts. Read after uncertainty; expired claims interrupt; late completions cannot overwrite newer versions. No scheduler, automatic retries or refunded uncertain calls.

Inherit server ownership/CSRF/fixed 24-hour expiry/reset. Changed sources invalidate affected answers/submissions. Validate known field/type/size/source/version/exact quote/span before save; separately evaluate semantic support. Source/model text is untrusted; escape values, keep keys server-side, no hidden-reasoning logs.

Keep transport, request rules, orchestration, provider I/O, validation and presentation distinct. Narrow adapters share typed error/cancellation contracts; provider changes cannot change submission authority. Two tools remain narrow, direct EF retained.

Cloudflare Workers AI remains unverified Free/no-card candidate, called directly by C# REST without another Worker. A0 checks eligibility, quality, latency, actual usage and CS-06 limits. Batch within bounds if needed; never silently raise them or enable billing. Existing app/database Free hosting remains baseline; local Ollama is not public hosting.

Historical sections below concern reusable existing-app evidence, not this feature's proof or product constraints.

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

The visitor enters one main OF-1042 walkthrough. **Try failure recovery** below it opens the independent OF-1043 fixture; **Back to main walkthrough** returns to saved main work. This is presentation over the existing shared workflow, not a second workflow implementation. View selection is transient and refresh defaults to the main path; API/database state remains separate and persisted. Project changes focus the destination heading, and distinct disclosure/panel keys preserve isolation when React remounts those views.

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

The implementation stays in the existing C# app with direct EF Core access and one React workflow panel; no new dependency, external service, queue or workflow framework was introduced. Each mutation locks the session row used by Reset, checks the owned project, validates the expected Onboarding version/current revision and rechecks expiry before commit. PostgreSQL constraints protect one onboarding per project, required-item kinds, revision/submission identity, correction comments, message keys and ready/snapshot consistency. Foreign-key ownership paths are indexed and cascade on sandbox reset.

Sample replies come from fixed server-owned fixtures and do not approve themselves. The original Pages contains Shop and is accepted into review; approval is rejected until the five signed pages are confirmed. The corrected Pages is a fresh revision, preserves the earlier comment/decision and retains unchanged Goals/Assets approvals. Review outcomes are terminal per revision. The saved brief includes approved revision identities/reviewers and approved content, and is read from that snapshot on refresh.

Welcome preparation and correction messages are saved in the labelled Outbox; Activity records actor, action, outcome and time with the business change. No message is delivered. New OF-1043 onboardings start with simulated welcome availability false; OF-1042 starts available. Restore and explicit Try welcome recover the former on attempt two. A maximum of three committed rows is enforced by the attempt number constraint and unique onboarding/number and onboarding/command indexes. Expected version/count and the session lock prevent distinct overlapping commands spending twice from one observed state.

The additive `Reliability` migration backfills every already-prepared welcome as successful attempt one, retaining its preparation time and existing output identities. Previously successful OF-1043 work remains successful; it is not retroactively failed. Pending OF-1043 work receives the unavailable flag. There is no reset or fresh budget during upgrade.

RegisterEntry has a unique Onboarding and stable `SIM-` reference, a JSONB copy of the approved brief and a confirmation time. Entry, confirmation and Activity commit together; readiness remains separately stored. Unknown welcome/publication responses require a read before another UI mutation, so a committed result is discovered without optimistic confirmation or a blind new attempt. Fault-injection tests use short-lived triggers only inside disposable test databases; no failure-injection subsystem exists in the app.

## Employer UI pass: implemented

The existing React App/OnboardingPanel and CSS now shorten the introduction and stage instructions, collapse project metadata with native details, and show sample/review/ready/handoff actions when relevant. Exact signed pages stay beside Pages review. Body/action text is 16px, supporting text at least 14px, and the main controls have a 44px minimum height. Existing tabs/details retain Outbox, Activity, earlier revisions and approval/register evidence.

Simple heading refs preserve orientation when replies arrive, answers change or readiness opens the brief. Error navigation keeps the Load saved state action visible until an uncertain response is resolved. The handoff control precedes the longer brief content. This slice changes no API, database schema, dependency or business rule; the nine-table model and same-origin architecture remain unchanged. Browser and visual evidence are in the harness. First-time human timing and public hosting acceptance are still pending.

## Focused code and API review on 19 October 2026

At Dexter's request, the existing workflow endpoint file was separated by responsibility. `WorkflowEndpoints` maps routes, reads the sandbox cookie and maps domain outcomes to HTTP responses. Scoped `WorkflowService` coordinates the existing direct-EF queries, ownership/expiry checks, transactions and saved effects. `WorkflowRules` holds pure eligibility/validation helpers and `WorkflowViewMapper` projects persisted state into response DTOs. `SandboxSessionAccess` shares token lookup between the session and workflow code. No generic repository, service interface, new dependency or schema was added. This makes the single-responsibility boundary clearer without claiming a formal SOLID certification or implementing unused abstractions.

`ApiSafety` bounds API traffic to six concurrent requests and 240 requests per minute per app instance, with no queue. Excess requests receive 429 with Retry-After; the UI reads saved state before a new mutation. Static assets are outside that limiter. The shared global budget is appropriate for this small demo but means visitors share its capacity; it is not DDoS protection. Kestrel caps request bodies at 16 KiB, with an early Content-Length rejection before session/database work. Production errors expose generic Problem responses, not exception details. API responses use no-store; nosniff, frame denial and no-referrer headers protect browser delivery. No new dependency was needed. See [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

The review uses the [OWASP API Security Top 10 (2023)](https://owasp.org/API-Security/editions/2023/en/0x11-t10/) and relevant session, input and HTTP controls from [ASVS](https://owasp.org/projects/asvs). It is a focused checklist with test evidence, not a complete ASVS assessment or security certification.

### Frontend responsibility separation on 19 October 2026

The former 873-line `OnboardingPanel.tsx` combined request/recovery state, view selection, review forms, results and history. It now coordinates navigation, form/focus state and explicit visitor actions. `onboarding/useOnboardingWorkflow.ts` owns loading, command responses, controlled errors, conflict reconciliation and the read-before-new-command guard after an uncertain response. Its initial-load/action callbacks let the panel control selection/focus without teaching the data hook about tabs or DOM refs.

`ReviewWorkspace`, `WelcomeRecovery`, `KickoffBrief`, `ActivityLog` and `SimulatedOutbox` render the existing sections with typed data and callbacks. Shared response types and date formatting live in `onboarding/workflow.ts`. The panel is 362 lines; the request hook is 100. The same elements, labels, keyboard behavior, correction rules and saved-state recovery remain under the existing browser suite. This is responsibility separation within the existing React application, with no state-management library, generic workflow framework, API/schema change or runtime-speed claim. The API/database remains authoritative.

### Browser security hardening on 19 October 2026

`ApiSafety` sends an enforced Content Security Policy for the built application. It starts with `default-src 'none'` and permits scripts, styles, images, fonts and API connections only from the same origin. Inline scripts/event handlers/styles and `eval` have no exceptions; objects, embedding, base-URL changes and native form submissions are denied. The application uses bundled local assets and fetch-based commands, so no third-party allowlist, nonce service or dependency is needed. This is an origin-allowlist CSP, not a nonce/hash-based strict CSP or a substitute for output encoding. See [OWASP CSP guidance](https://cheatsheetseries.owasp.org/cheatsheets/Content_Security_Policy_Cheat_Sheet.html).

ASP.NET Core's HSTS middleware runs after verified forwarding and advertises `max-age=86400` on non-development HTTPS responses. The initial one-day browser policy follows [Microsoft's rollout guidance](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0#http-strict-transport-security-protocol-hsts). Each qualifying response renews it; this is not an app or deployment-key expiry. Default loopback exclusions remain. No `includeSubDomains` or preload is requested for this provider-owned hostname. Deplexo's existing HTTP-to-HTTPS redirect handles HTTP entry; HSTS is learned after HTTPS and is not a first-visit preload guarantee.

Production-policy tests cover documents, static assets, API errors, local-development exclusions and trusted/untrusted forwarding. Browser tests exercise real CSP rejection of injected inline code, `eval` from a normally loaded same-origin script and external fetches. The complete desktop/phone workflow also asserts no CSP violations. Delivery checks the live document and API headers as well as the expected revision and persisted workflow, so a successful source build alone cannot mark the release accepted.

Hosted HTML inspection also revealed an automatically injected Cloudflare Web Analytics beacon. HTML responses now use `Cache-Control: no-cache, no-transform`: they must be revalidated, and Cloudflare's [documented no-transform behaviour](https://developers.cloudflare.com/web-analytics/get-started/) prevents analytics injection. This preserves the same-origin policy without adding a third-party script exception; static assets retain their existing caching behaviour and API responses remain no-store. The live smoke requires no-transform and rejects external script sources in the delivered HTML. No Cloudflare security feature or account setting is disabled.

These controls reduce browser attack opportunities. They cannot change Webroot/BrightCloud or other vendors' reputation decisions. Dexter reported submitting the Webroot review request on 19 October 2026; its clearance remains unconfirmed. Google Safe Browsing's exact-host report displayed "No unsafe content found" on that date. That point-in-time result does not guarantee every visitor/security product will accept the hostname. Never ask employers to bypass a warning or disable protection.

Delivery adds a generated public `/release.json` containing only the source commit and repository URL. It is a static release marker, not a diagnostic or privileged API. GitHub Actions packages committed files with this marker, builds the package before deployment, validates its SHA-256, delivers it to the existing app and verifies the live marker plus database-backed secure workflow. The host builds the package again; this is source-artifact delivery rather than promotion of an identical image digest. Pinned actions/base-image digests and npm/NuGet locks control inputs. Only the trusted main job receives the scoped deployment token; PostgreSQL credentials remain in Deplexo. README.md describes operation; HARNESS.md records actual run evidence.

| Risk area | Implemented boundary and evidence |
| --- | --- |
| API1 / API3 / API5: object, property and function access | Server-owned project/session lookup on every workflow route; typed command DTOs do not accept an actor or arbitrary model update. Cross-visitor and revision-ownership negative tests remain passing after separation. There is one visitor role and no administration API. |
| API2: session authentication | Random 256-bit sandbox token, only its hash stored; Secure/HttpOnly/Strict cookies, antiforgery on POSTs, fixed server-side 24-hour expiry and reset revocation. This is a fictional sandbox capability, not a real-customer authentication system. |
| API4 / API6: resource use and sensitive flows | API concurrency/rate/body limits, server/database repeat protection, committed three-attempt welcome budget and version checks. Dedicated Production-policy tests cover rejection without blocking static assets. |
| API7 / API10: URL fetches and external consumers | Visitor content cannot trigger URL fetching, email, Make or Google Sheets writes. Asset references and handoff are labelled samples/simulation. A later real integration needs its own review. |
| API8 / API9: configuration and inventory | Production errors, verified PostgreSQL TLS, exact proxy trust and strict scheme evidence; route inventory above, no diagnostic endpoint. Removed temporary raw-header logging and reduced SQL/debug logging. Production dependency advisory checks are recorded in HARNESS.md. |

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

Dexter clarified on 19 October 2026 that **no credit-card verification** is part of the free-hosting requirement. Northflank fails it and is excluded. He requested open source alternatives using OpenAlternative and confirmed that no existing server or spare computer is available. Free software alone cannot establish an available, no-cost public runtime.

| Candidate | Finding and planning status |
| --- | --- |
| Northflank Sandbox | Rejected: a payment card is required before creating even free app/database resources. The authorised setup left an empty Free team/Default project in US Central, with no card or app/database/job. It is no longer a deployment candidate. |
| Render Free | Excluded: its documented idle behaviour can produce an approximately one-minute wake-up, which Dexter rejected. Its free PostgreSQL also expires after 30 days and does not meet the ongoing-free requirement. |
| Coolify / Dokploy self-hosted | OpenAlternative lists both as deployment alternatives. Official docs confirm free self-hosted software, Docker/application/database support and a required user-provided Linux server. They could run this stack, but do not solve the missing compute. Do not install a dashboard or recommend renting a server under the current constraints. |
| alwaysdata Free | Earlier candidate, now unconfirmed and activation on hold. Published .NET/PostgreSQL support and local resource fit do not resolve the billing-clause applicability or prove hosted startup. See the dated evidence below. |
| Neon Free | Previously noted database-only alternative; it does not host the C# app and was not revalidated in this pass. Do not add another provider unless the combined host/database candidate fails a concrete check. |

### No-card candidate: checks before selection

Alwaysdata passes the published-stack check and a local resource rehearsal. It is not yet an accepted public host. Its Free plan is for non-profit use, restricts website addresses to the account's `alwaysdata.net` address and excludes background Services. This fictional, non-selling personal portfolio appears consistent with the personal-use offer; that is an eligibility inference, not provider confirmation. Verify the actual free account exposes the documented .NET/PostgreSQL features. The native .NET website type can run the single application directly.

On 19 October 2026 the existing framework-dependent Release publish was **7.76 MiB**. A local Linux .NET 10.0.13 Production process, limited to **256 MiB with no swap and 0.25 CPU**, completed onboarding, correction, fresh approval, kickoff brief, repeated handoff and welcome recovery on attempt two against PostgreSQL 17.11. Peak cgroup memory was **164.84 MiB**, with no memory-limit/OOM events. The exercised disposable database occupied **8.05 MiB**. From process launch to HTML, CSRF, sandbox creation and usable project/onboarding API data took **4.78 seconds**. This is a local server/API measurement, not browser rendering, provider startup or a concurrency/load guarantee. HARNESS.md records method and retained evidence.

The app measurement excludes the separate PostgreSQL server process and provider infrastructure. The small publish/database footprint is promising for 1 GB shared storage, not proof of long-term account consumption. Confirm the provider's actual app/database CPU/RAM accounting and inspect disk use including logs, database growth and expired-session cleanup. Preserve C#/PostgreSQL; no rewrite, AOT conversion, extra deployment platform or artificial keep-alive system is needed for this rehearsal. No alwaysdata account was opened or app uploaded.

Without a card or payment the account remains functional in **restricted mode**: stricter HTTP filtering can add request latency, and SMTP is limited. The app sends no real mail. Keep restricted mode and test real GET/POST/cookie requests; adding a card or payment to lift it is excluded. Free profiles also require an administration login every **120 days in the first year**, every six months at ages one to four, then annually. Visitor traffic is not a substitute stated by that policy. No paid plan, domain purchase or paid fallback is proposed.

The website docs describe configurable idle time, including zero, but warn processes can still stop. Their 24/7 background-Service workaround is unavailable on Free. Verify free-account settings and measure actual usable fresh/post-idle entry; no no-sleep or latency guarantee is established. If eligibility, resource limits or startup fails, report that precise blocker before choosing another provider.

The signed-in first-hosting-account form defaults to paid Small; Free must be selected explicitly. On 19 October 2026 Free was selected with proposed name `dexter-onboardflow`, but not submitted. The form requires a default hosting-user password and acceptance of [shared-hosting contract 28](https://www.alwaysdata.com/en/contract/28/) and [terms 36](https://www.alwaysdata.com/en/contract/36/). Clause 2.1.3 of contract 28 covers resource upgrades at applicable fees after notification and provides for refusal/termination. No clear Free exemption was established from the reviewed published billing pages. This is an unresolved applicability question, not evidence that Free accounts are actually charged. Do not equate no card with a hard spending cap or accept a possible paid-upgrade commitment under the current instruction. Dexter declined contacting anyone on 19 October 2026; do not send the drafted support question. The hosting password and contract acceptance remain subject to the browser's credential/terms confirmation rules. Full suitability is **not confirmed**: published compatibility/local resource fit do not prove hosted speed or the unresolved zero-cost condition.

Dexter earlier authorised this bounded setup/hosted test on 19 October 2026. The later reassessment below puts activation on hold; these are retained technical preparation steps, not the current execution instruction:

1. Create/select the no-card **Free** account, verify its use restrictions/quotas and use its free HTTPS `alwaysdata.net` address.
2. Build locally and upload only the framework-dependent publish, including React in `wwwroot`. Select .NET 10, run the published DLL as a native .NET website from its publish directory, and bind the assigned IP/port. No on-host SDK build, Vite server, Dockerfile or second frontend host is needed.
3. Create managed PostgreSQL 17, supply the connection through protected environment settings and apply reviewed migrations. Start with the documented direct PostgreSQL connection (port 5432); no pooler or extra database provider is required. Verify schema privileges and provider connection-security requirements.
4. Confirm HTTPS/proxy handling and a persistent writable home for ASP.NET data-protection keys, then run the hosted acceptance below in restricted mode. Inspect account memory/disk during the real journey. Share the employer URL only after review.

Alwaysdata's [HTTP stack](https://help.alwaysdata.com/en/docs/web-hosting/sites/http-stack/) terminates HTTPS and forwards HTTP with `X-Forwarded-Proto`. `Program.cs` now processes that scheme header using ASP.NET's trusted-proxy middleware, retaining its loopback defaults and allowing exact additional proxy addresses through `ReverseProxy__KnownProxies__0` (then `__1`, etc.). Set only provider-verified proxy addresses; do not trust all forwarders. Three Production checks prove trusted HTTPS obtains Secure/HttpOnly/SameSite cookies while unknown proxies or missing HTTPS cannot obtain them. Actual provider proxy address, CSRF/session writes and key persistence still need hosted verification. Follow [Microsoft's guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0); do not use Development or weaken cookies to force a pass.

After deployment is authorised, measure fresh and post-inactivity entry from navigation until project data and Start are usable, then verify the first action reaches PostgreSQL. Compare warm entry, at least an hour idle and an overnight interval, without keep-alive pings. A roughly minute-long wait fails AC-10 even if a page shell appears quickly. No hosted result is claimed.

If this route is resumed after review, keep it to the bounded Free setup/hosted check and stop before employer distribution. No new application feature, architecture redesign or deployment-tool installation is required. The older Workers/D1 assumptions remain historical.

## Hosting reassessment and website entry on 19 October 2026

Dexter challenged recommending two services before establishing the whole fit. No additional account or resource was created in this reassessment and nobody was contacted. A free plan is insufficient evidence on its own: check app/runtime, PostgreSQL, HTTPS, no-card eligibility, recurring quotas and limit behaviour, ordinary sleep and extended-idle wake-up before suggesting setup. Actual app speed still requires a hosted acceptance test after a suitable candidate clears that desk check.

| Additional route checked | Finding before signup |
| --- | --- |
| Koyeb / Modal | Their official billing documentation requires a payment method. Excluded under the no-card condition. [Koyeb](https://www.koyeb.com/docs/faqs/pricing), [Modal](https://modal.com/docs/guide/billing). |
| Railway | Published Free has $1 recurring monthly usage allowance. Serverless documentation warns the first request after sleep can return 502. No verified monthly fit or acceptable cold entry exists for this app. Not selected. [Plans](https://docs.railway.com/pricing/plans), [idle behaviour](https://docs.railway.com/guides/cut-idle-costs-serverless). |
| MonsterASP | Published Free includes current .NET and PostgreSQL, but enabling HTTPS requires a support request. That conflicts with Dexter's no-contact instruction. Not selected. [Free restrictions](https://help.monsterasp.net/getting-started/first-steps/free-vs-premium). |
| blitz.cloud | Its published Free plan explicitly needs no card, has no automatic paid conversion and includes Docker, HTTPS and PostgreSQL 17. However, database documentation says a full day asleep puts the account disk away and the next wake can add one or two minutes. This fails the post-idle requirement for the bundled arrangement; do not recommend signup from the shorter ordinary-wake claim. [Pricing](https://blitz.cloud/pricing/), [database sleep](https://blitz.cloud/docs/databases/), [terms](https://blitz.cloud/terms/). |
| Back4app Containers | Pricing advertises a no-card Free container. Its newer engineering article reports temporary URLs and destruction after 60 minutes on Free. Not a verified ongoing demo route; no signup recommended. [Pricing](https://www.back4app.com/pricing/container-as-a-service), [reported Free lifetime](https://www.back4app.com/blog/run-a-bot-webhook-or-cron-24-7-without-a-vps). |
| Leapcell / Zeabur | Free offerings were inspected, but the complete .NET/no-card/recurring-runtime arrangement was not established. Incomplete evidence is not a recommendation. [Leapcell](https://leapcell.io/pricing), [Zeabur](https://zeabur.com/pricing). |

**Earlier outcome: no verified full-stack host.** The follow-up below adds a concrete recommendation after further published-plan and local resource checks. Alwaysdata remains unconfirmed rather than a proven runtime failure. Preserve its unsubmitted setup and the reviewed application; no rewrite or paid fallback is authorised.

The delivery shape is settled: **dexter-developer.dev project card → Try this demo → public HTTPS OnboardFlow app → PostgreSQL**. GitHub is a separate adjacent link. The portfolio host serves the card; the app host serves both React and the C# API under one origin. A provider hostname is sufficient, avoiding a custom-domain dependency. Verify this end-to-end click and the first database-backed action before the button is published. A video, static case study or browser-only simulation can supplement the real app, but substituting one for the interactive full-stack demo needs Dexter's explicit scope decision.

## Recommended no-card route checked on 19 October 2026

**Authorised route: Deplexo Free for the combined React/C# app and Neon Free for PostgreSQL.** Published-plan/local-fit checks below passed. Dexter completed signup/sign-in himself and both dashboards confirm Free. The separate PostgreSQL 17 project in Frankfurt is migrated and capped at 0.25 CU. The Deplexo app runs with JSON config reload disabled to avoid the worker's shared inotify limit. The secure workspace failure is now resolved and bounded hosted workflow/security checks pass. HARNESS.md records the evidence and remaining extended-idle/release gates; no bypass or alternate access method to evade a warning is permitted.

| Requirement | Checked evidence and remaining limit |
| --- | --- |
| C# runtime | Deplexo supports a custom Dockerfile. The existing framework-dependent app ran unchanged in the official .NET 10 Alpine runtime during the local rehearsal. No API rewrite or new application dependency is needed. [Container requirements](https://docs.deplexo.com/guides/docker/). |
| Ongoing free app, no card | Free is $0 with one app, 0.25 CPU, 128 MB RAM, 250 MB disk and 100 GB monthly transfer. Payment details are unnecessary to run it. At transfer exhaustion apps stop; there is no metered overage charge. RAM exhaustion can kill/restart a process, so local fit was tested first. [Plans](https://deplexo.com/plans), [limit behaviour and external databases](https://deplexo.com/alternative/railway). |
| App wake | Deplexo explicitly says Free containers keep running and do not idle-sleep. This removes a scheduled wake delay; shared-resource speed, restarts and real browser entry still require hosted measurement. Free has no uptime guarantee. [Runtime policy](https://deplexo.com/about), [terms](https://deplexo.com/terms). |
| PostgreSQL, no card | Neon Free costs $0 without a card or time limit. Current pricing includes 1 GB storage, 100 CU-hours and 5 GB outbound transfer per project. PostgreSQL 17 is supported. Keep one project/branch and the smallest compute with normal scale-to-zero. Do not enable paid backend products. [Pricing](https://neon.com/pricing), [version support](https://neon.com/docs/postgresql/postgres-version-support). |
| Database wake and quotas | Neon suspends idle compute after five minutes and documents automatic resume within a few hundred milliseconds. Free compute/transfer exhaustion suspends access until the next billing period; storage-growing writes fail at the cap. Free has no overage billing. No keep-alive jobs. These published figures do not measure our app's hosted latency. [Scale-to-zero](https://neon.com/docs/introduction/scale-to-zero), [quota enforcement](https://neon.com/faqs/free-plan-limits-and-quotas). |
| Connection, HTTPS and files | Deplexo explicitly recommends external Neon via environment variables; ordinary Npgsql TCP/TLS is the intended path. HTTPS subdomains are included. Its root filesystem is read-only, with writable temporary space and a worker-local /data volume. Set HOME to /data so ASP.NET keys have a writable persistent home; verify persistence after restart and actual proxy addresses/HTTPS handling before acceptance. [External database](https://deplexo.com/alternative/railway), [free HTTPS](https://deplexo.com/free-hosting), [filesystem](https://docs.deplexo.com/operations/storage/). |

The local Alpine rehearsal used **128 MiB RAM, no swap, 0.25 CPU, non-root, read-only root, dropped capabilities and 100 MiB no-exec temporary space**. Main/repeat/correction/brief/publication and recovery checks passed, plus concurrent saved-state reads: **24 successful requests, 81.96 MiB peak and no memory-limit/OOM events**. Base runtime plus publish is **184.72 MiB**, below even a conservative interpretation of 250 MB disk; this is a sum of measured components, not a built production-image measurement. Database footprint was **8.05 MiB**. Local process-to-usable-API entry was **4.65 seconds**; it is not hosted/browser timing. See HARNESS.md for retained evidence.

The authorised setup allocated one Free app container and one Neon PostgreSQL 17 database, using source ZIP upload and protected environment settings. Reviewed migrations are applied. A diagnostic before forwarded-header processing identified `X-Forwarded-Proto=http`, `X-Forwarded-Port=80` and `CF-Visitor={"scheme":"https"}` from exact peer `::ffff:10.92.13.1` during a public HTTPS visit. Cloudflare documents this JSON scheme header; browser probes confirmed the edge replaces a visitor-supplied false value. [Cloudflare header reference](https://developers.cloudflare.com/fundamentals/reference/http-headers/#cf-visitor).

The default-off `ReverseProxy:UseCloudflareVisitor` adapter is explicitly enabled for that verified path. It removes any incoming derived header, accepts only one valid HTTP/HTTPS scheme from an explicitly configured peer, then supplies a private scheme header to ASP.NET's existing trusted-proxy middleware. Missing, malformed or untrusted evidence fails closed. Production, antiforgery, Secure cookies and verified PostgreSQL TLS are preserved. No new dependency, schema, business feature or blanket network trust was added. HARNESS.md records actual browser/API/Neon passes, restart verification, timings and resource samples. Hour-idle/overnight entry, sustained capacity and first-time human timing remain open. No employer URL is published until acceptance and review.

Other fresh checks: ClawCloud Run's former recurring-free offer is unavailable; its [official discontinuation notice](https://question.run.claw.cloud/questions/10010000000003261) ended free services on 11 May 2026. Velixir's free app cannot reach an external PostgreSQL port without a support request, while its own managed PostgreSQL is paid ([network rules](https://velixir.net/docs/limits)); it does not satisfy the current no-contact arrangement. DockHosting's public pages make relevant free/container/fast-wake claims, but database storage/idle details and the reachable dashboard were not established; no signup recommendation was made. These routes are not needed for the proposed Deplexo/Neon slice.

Dexter also asked about Render again. Its [current Free documentation](https://render.com/docs/free) still says 15-minute app idle sleep, about one minute to wake, and free PostgreSQL expiry after 30 days. Pairing it with Neon fixes expiry only. It remains excluded under the unchanged startup requirement.

## Planning checkpoint and execution boundary

This data/state design completed checkpoint CP-1 in [HARNESS.md](../HARNESS.md). Foundation, the main slice, reliability/simulated handoff and employer UI improvements are implemented without another general planning round. Stop for review. The application shape, fixed 24-hour lifetime and three-attempt limit remain unchanged.

The implemented migrations, routes and evidence are recorded above and in the harness. Hosting repair and the bounded quality/API review are authorised; employer distribution and source publication remain behind review. Dexter requested a practical bounded release check instead of hours of manual testing. Extended-idle, human timing and sustained uptime remain unmeasured limits, not reasons to start another feature or architecture milestone. Portfolio edits/publication belong to the other chat. Surface a material conflict rather than silently broadening scope.

Hosting sources checked on 19 October 2026: [OpenAlternative discovery list](https://openalternative.co/alternatives/render), [Coolify self-hosting requirements](https://coolify.io/docs/start-with-self-hosted), [Coolify runtime/server responsibility](https://coolify.io/docs/core/how-coolify-works), [Dokploy self-hosted/cloud distinction](https://docs.dokploy.com/docs/core/differences), [Dokploy server requirements](https://docs.dokploy.com/docs/core/installation), [alwaysdata no-card free offer](https://www.alwaysdata.com/en/), [free limits and restrictions](https://help.alwaysdata.com/en/docs/admin-billing/billing/public-cloud-prices/), [.NET versions](https://help.alwaysdata.com/en/docs/web-hosting/languages/dotnet/), [native .NET configuration](https://help.alwaysdata.com/en/docs/web-hosting/languages/dotnet/configuration/), [PostgreSQL versions/connections](https://help.alwaysdata.com/en/docs/web-hosting/databases/postgresql/), [idle behaviour](https://help.alwaysdata.com/en/docs/web-hosting/sites/misc/), [no-card restricted mode](https://help.alwaysdata.com/en/docs/admin-billing/profile/restricted-mode/), [free-profile inactivity suspension](https://help.alwaysdata.com/en/docs/admin-billing/profile/suspension/), [Northflank card requirement](https://northflank.com/docs/v1/application/billing/pricing-on-northflank), [Render Free](https://render.com/docs/free). Neon was not rechecked in this pass.

Stack references: [ASP.NET Core static files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0), [.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [Npgsql EF Core provider](https://www.npgsql.org/efcore/), [Npgsql 10.0](https://www.npgsql.org/efcore/release-notes/10.0.html).

Data-integrity references: [PostgreSQL constraints](https://www.postgresql.org/docs/current/ddl-constraints.html), [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
