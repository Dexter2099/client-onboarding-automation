# OnboardFlow

A client-onboarding app based closely on [this Content Snare client demo](https://demo.contentsnare.com/shared/21481036-b8ca-4abb-ac0b-4d09b663333a/l/instructions), with an AI agent drafting answers from fictional client documents.

React/TypeScript, C# / ASP.NET Core / .NET 10 and PostgreSQL. OpenAI runs on the server through a bounded adapter. The dedicated project has a US$5 monthly limit; a persistent PostgreSQL ledger stops new inference at US$4.50. The separate portfolio chatbot key is unrelated.

## Try the demo

[Open the current hosted demo](https://dexter-onboardflow.de.deplexo.com/).

**Hostless migration in progress:** the replacement uses the verified no-card free allocation of 0.25 vCPU / 512 MB, one always-running instance, maximum one instance and review apps disabled. The existing demo stays available until Hostless passes the full HTTPS journey and the first visit after at least one hour without visitor traffic. Published no-sleep behaviour is not a measured speed result. [Current release evidence](HARNESS.md).

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

The free target is **0.25 vCPU, 512 MB, minimum one instance, maximum one instance, review apps off**, with no card, paid plan or credit dependency. Keep all configured resources within the account's shared free pool. Hostless's spending-limit field is not a hard invoice cap; entering zero disables that control. Use the free resource limits rather than relying on that field.

The container reads Hostless's `PORT`, binds on all interfaces and exposes `/health` without touching the database. Production HTTP redirects to HTTPS. Only the verified ingress peer may supply the protocol header; the host overwrites visitor-supplied values. Workstation GC has a 256 MiB managed-heap ceiling within the 512 MB container. Fonts, licences, the labelled MP4/WebM recording and its poster are packaged locally. Startup secrets belong only in server settings:

- `ConnectionStrings__OnboardFlow`: the existing production Neon database, retaining the shared spending ledger.
- `Intake__Enabled=true` and `Intake__Provider=OpenAI`.
- The dedicated OnboardFlow project ID, verified-budget flag and restricted server-side Responses key.
- Reverse-proxy settings verified against the actual ingress; never trust arbitrary forwarded headers.

A separate credential-free ingress probe verifies the new host before the application secrets are moved. It is a deployment diagnostic, not simulated AI or the employer demo. The production Dockerfile does not include it. Cutover requires source/clarification/manual submissions, saved reload/restart, desktop/mobile playback and actual entry after inactivity to pass over HTTPS. Keep the previous release available for rollback; do not roll back the additive schema or the spending ledger.

## Project references

| Path | Responsibility |
| --- | --- |
| `src/OnboardFlow.Api/ClientRequests` | Fixed fictional schema/sources, store, rules, model adapter, agent and endpoints. |
| `preview/content-snare/src` | Current interface and typed same-origin API client. |
| `tests/OnboardFlow.Api.Tests` | Rules, API/security checks, browser integration and opt-in real-model cases. |
| [PLAN.md](PLAN.md) | Scope, acceptance and limits. |
| [Architecture](docs/architecture.md) | Integration and design boundaries. |
| [Walkthrough](docs/employer-walkthrough.md) | Visitor journey. |
| [HARNESS.md](HARNESS.md) | Actual checks, measurements, failures and release evidence. |
| [AGENTS.md](AGENTS.md) | Development guardrails and current authorisation. |

The earlier manager/workflow prototype remains in Git history. It does not describe the current client-request interface. The portfolio website and a replacement recording from the hosted app are separate work.

[MIT licence](LICENSE). Dependencies retain their own licences; [third-party notices](THIRD_PARTY_NOTICES.md).
