# OnboardFlow

An AI onboarding app that drafts answers from fictional client documents, shows supporting passages and asks for missing details before human submission.

React/TypeScript, C# / ASP.NET Core / .NET 10 and PostgreSQL. OpenAI runs on the server through a bounded adapter. The dedicated project has a US$5 monthly limit; a persistent PostgreSQL ledger stops new inference at US$4.50.

## Try the demo

[Open OnboardFlow](https://onboardflow.hostless.app/).

Hostless runs the app on its verified **free, no-card 0.25 vCPU / 512 MB allocation**, with one always-running instance, maximum one and review apps disabled. The complete HTTPS AI journey, sources, clarification, human submissions, desktop/mobile layouts, macOS WebKit and saved progress across container replacement have been checked. Long-idle performance has not been measured. [Release evidence and practical limits](HARNESS.md).

Two fresh-browser checks on the active service reached usable request data in **2.26–5.35 seconds** and saved the first edit in about **0.54 seconds**. These are two observations from one machine/network. [Raw measurements and checked revision](docs/evidence/hostless-release-2026-10-25.json).

1. Select Let's get started, then Fill with AI.
2. Inspect a supporting passage with View source.
3. Use the labelled sample reply for the missing fictional tax reference, then explicitly continue filling.
4. Complete the four manual-only sample attachment/confirmation items.
5. Submit individual answers or Submit page for review. All 23 items finish awaiting human review.

AI never submits or approves, uploads files, grants access or makes payments. Editing clears extraction attribution and requires resubmission. PostgreSQL stores drafts, questions, run state and allowance. Refresh loads saved work without starting another AI run. The Instructions video is labelled as the earlier local Ollama recording.

## Architecture and spending controls

| Part | Implementation |
| --- | --- |
| Interface | React/TypeScript; Instructions, 11 information fields, 12 tasks, nested navigation and page summaries. |
| API and agent | ASP.NET Core; source validation, strict output, two read-only tools and a bounded model loop. |
| Storage | PostgreSQL / EF Core / Npgsql; owned 24-hour sandboxes and persisted request state. |
| Inference | OpenAI Responses, pinned evaluated GPT-5.4 snapshot; no automatic provider/model fallback or retry. |
| Run limits | Six model calls including continuation, 512 output tokens per call, 15-second call timeout, three runs per sandbox. |
| Monthly limit | Reserve counted input plus output allowance before each call. Settle conservatively; retain uncertain holds. Stop at US$4.50. |
| Human boundary | Fictional documents and sample actions; submissions require the visitor's explicit action. |

The ledger is shared across hosts and survives sandbox resets, expiry, restarts and releases. Migration reuses the same production Neon database; it does not reset the budget or import an older counter. When inference is unavailable, saved work and manual completion remain available with an honest availability message. The model sees fictional source material, not visitor credentials.

## Engineering choices

The agent depends on the small [IIntakeModelClient](src/OnboardFlow.Api/ClientRequests/IIntakeModelClient.cs) contract. OpenAI, Cloudflare and Ollama implement that boundary; provider selection lives in the dependency-injection composition root. The shared [tool protocol](src/OnboardFlow.Api/ClientRequests/IntakeModelProtocol.cs) is independent of those adapters. Every result passes the same application validation before it can become a draft.

[IntakeAgentService](src/OnboardFlow.Api/ClientRequests/IntakeAgentService.cs) owns orchestration, [IntakeRules](src/OnboardFlow.Api/ClientRequests/IntakeRules.cs) owns pure validation, [IntakeStore](src/OnboardFlow.Api/ClientRequests/IntakeStore.cs) owns transactional state/command receipts, and [IntakeSpendBudget](src/OnboardFlow.Api/ClientRequests/IntakeSpendBudget.cs) owns the shared monetary policy. Injected HTTP clients and time make these boundaries testable. Comments explain security decisions, transaction locks, uncertain holds and release integrity. The tests and release evidence below demonstrate these choices in practice.

The frontend separates [request navigation](preview/content-snare/src/RequestNavigation.tsx), [typed field rendering](preview/content-snare/src/RequestFieldInput.tsx), [Instructions/media](preview/content-snare/src/Instructions.tsx) and [source inspection](preview/content-snare/src/SourceDocument.tsx) from application state and commands. The source view owns native dialog focus/Escape behaviour; Instructions owns playback state. [IntakeApi](preview/content-snare/src/api.ts) serializes commands and handles session/CSRF/version state. Components receive explicit data and callbacks rather than creating their own API clients.

Content-hashed assets use immutable public caching; HTML is revalidated and private API responses are never cached. A regression check enforces those separate policies. Performance checks measure loaded request data and a saved edit in a fresh browser, with the raw timings retained.

## Run locally

Prerequisites: Node.js 24/npm, the .NET SDK in `global.json`, PowerShell 7, Docker, and Ollama with `qwen3.8:27b` for local inference.

```powershell
npm ci
pwsh -NoProfile -File scripts/intake-local.ps1 setup
```

Start the local API and interface in separate terminals:

```powershell
pwsh -NoProfile -File scripts/intake-local.ps1 api
```

```powershell
node node_modules/vite/bin/vite.js --config preview/content-snare/vite.config.ts
```

Open **http://127.0.0.1:5622/l/instructions**. Vite proxies to the C# API on loopback 5080; C# calls Ollama on loopback 11434. Keep those processes running. If Ollama is not already running, start it with `OLLAMA_HOST=127.0.0.1:11434` and `OLLAMA_NO_CLOUD=1`.

Ordinary local development uses Ollama. Local paid inference against a separate ledger is blocked after the production cutover. Never copy a production key into `VITE_*` variables, browser code, Git, images or test reports.

## Build and verify

```powershell
npm run build
npm run format:check
npm run verify
```

The normal build packages the current interface into the ASP.NET static files. `npm run verify` runs backend checks plus 12 Chromium/Firefox/WebKit checks against a real Kestrel server, PostgreSQL and a test-assembly-only model double. Ordinary CI does not call paid inference. Stop a running local API before rebuilding on Windows to avoid assembly locks.

Real-model evaluation uses the same nine complete, missing/resumed and conflicting-source cases, three repetitions each. The evaluated model passed 9/9 before the first production release. Evaluation timings, conservative costs, failures and limitations are in [HARNESS.md](HARNESS.md). A small fixed matrix does not establish general reliability.

## Hostless deployment

Hostless builds Git repositories, so CI publishes the **verified source archive**, including its original revision marker and excluding workflow definitions, to the managed `deploy/hostless` branch. It never publishes ignored local files or credentials. The branch has normal fast-forward history; application changes are reviewed on `main` and must pass verification first. Provider deployment tracks this managed release branch. Documentation-only updates do not restart the runtime.

Live acceptance on Ubuntu and macOS checks normal HTTPS and forged protocol headers, plus HTTP redirects without cookies. Exact ingress peers are configured from observed provider traffic; a release-marker warning records canonical scheme/peer information if forwarding stops working. The gate requires the actual source revision and never follows redirects to make a check pass.

The free target is **0.25 vCPU, 512 MB, minimum one instance, maximum one instance, review apps off**, with no card, paid plan or credit dependency. Keep all configured resources within the account's shared free pool. Hostless's spending-limit field is not a hard invoice cap; entering zero disables that control. Use the free resource limits rather than relying on that field.

The container reads Hostless's `PORT`, binds on all interfaces and exposes `/health` without touching the database. Production HTTP redirects to HTTPS. Only the verified ingress peer may supply the protocol header; the host overwrites visitor-supplied values. Workstation GC has a 256 MiB managed-heap ceiling within the 512 MB container. Fonts, licences, the labelled MP4/WebM recording and its poster are packaged locally. Startup secrets belong only in server settings:

- `ConnectionStrings__OnboardFlow`: the existing production Neon database, retaining the shared spending ledger.
- `Intake__Enabled=true` and `Intake__Provider=OpenAI`.
- The dedicated OnboardFlow project ID, verified-budget flag and restricted server-side Responses key.
- Reverse-proxy settings verified against the actual ingress; never trust arbitrary forwarded headers.

The observed Hostless peers are configured as `ReverseProxy__KnownProxies__0=10.0.1.200` and `ReverseProxy__KnownProxies__1=10.0.7.101`, with `ReverseProxy__UseCloudflareVisitor=false`. The second address was identified after a CI request reached another ingress and correctly failed closed rather than trusting its scheme without configuration.

The separate credential-free ingress probe was used before secrets were moved; it is excluded from the production image. Release checks cover source/clarification/manual submissions, saved reload/container replacement and desktop/mobile playback over HTTPS. The previous Deplexo release remains available for rollback, sharing the same database and ledger. Long-idle testing is outside this release's acceptance scope.

## Project references

| Path | Responsibility |
| --- | --- |
| `src/OnboardFlow.Api/ClientRequests` | Fixed fictional schema/sources, store, rules, model adapter, agent and endpoints. |
| `preview/content-snare/src` | Current interface and typed same-origin API client. |
| `tests/OnboardFlow.Api.Tests` | Rules, API/security checks, browser integration and opt-in real-model cases. |
| [PLAN.md](PLAN.md) | Scope, acceptance and limits. |
| [Architecture](docs/architecture.md) | Integration and design boundaries. |
| [Walkthrough](docs/employer-walkthrough.md) | Visitor journey. |
| [Case study](docs/case-study.md) | Problem, engineering decisions and QA evidence. |
| [HARNESS.md](HARNESS.md) | Actual checks, measurements, failures and release evidence. |
| [AGENTS.md](AGENTS.md) | Development guardrails and current authorisation. |

The client interface follows [this Content Snare demo](https://demo.contentsnare.com/shared/21481036-b8ca-4abb-ac0b-4d09b663333a/l/instructions). The earlier manager/workflow prototype remains in Git history. The portfolio website and a replacement recording from the hosted app are separate work.

[MIT licence](LICENSE). Dependencies retain their own licences; [third-party notices](THIRD_PARTY_NOTICES.md).
