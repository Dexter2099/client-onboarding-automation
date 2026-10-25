# OnboardFlow

A client-onboarding application based closely on [this Content Snare client demo](https://demo.contentsnare.com/shared/21481036-b8ca-4abb-ac0b-4d09b663333a/l/instructions), with an AI agent drafting answers from fictional client documents.

**Release build:** React/TypeScript, C#/.NET 10 and PostgreSQL, with server-side OpenAI inference. The dedicated project enforces US$5/month; a persistent database ledger cuts off the app at US$4.50. Nine real-model evaluation cases, 130 backend checks and 12 browser checks across Chromium, Firefox and WebKit pass. [Verification and setup](HARNESS.md) · [Delivery and release evidence](https://github.com/Dexter2099/client-onboarding-automation/actions/workflows/delivery.yml).

## Try OnboardFlow

[Open the hosted demo](https://dexter-onboardflow.de.deplexo.com/). It uses fictional documents and actions. AI availability is limited by the shared monthly allowance; saved progress and manual completion remain available when inference stops. The labelled Instructions recording shows the earlier local Ollama run.

1. Select Let's get started, then Fill with AI.
2. The model requests field definitions and documents through two read-only tools, then proposes supported drafts.
3. View source beside an answer to inspect the supporting passage.
4. Supply the labelled sample clarification for the missing fictional tax reference, then explicitly continue filling.
5. Complete the four manual-only sample attachment/confirmation items yourself.
6. Submit individual answers or Submit page for review. All 23 items finish awaiting review.

AI does not submit/approve or complete actions on your behalf. Editing clears extraction attribution and requires resubmission. PostgreSQL stores drafts, questions, run state and allowance; refresh reads saved work without starting inference. Attachment/payment/access/message actions remain explicitly simulated.

## Technology and limits

| Part           | Implementation                                                                                                                                                   |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UI             | React/TypeScript; Instructions, 11 information fields, 12 tasks/eight groups, nested navigation and summaries.                                                   |
| API and agent  | C# / ASP.NET Core / .NET 10; request rules, transactional storage, bounded tool/model loop and pure source validation.                                           |
| Database       | PostgreSQL / EF Core / Npgsql; owned expiring sandbox and versioned request aggregate.                                                                           |
| Real inference | OpenAI Responses with pinned GPT-5.4; explicit provider selection, strict output/source validation and no automatic fallback. Local Ollama remains available. |
| Controls       | Six calls per run including continuation; 512 output tokens per call; 15-second call timeout; three runs per sandbox. No automatic provider retry/paid fallback. |

Model values replace canned browser extraction. Unknown tools/fields, manual-action proposals and invalid sources are rejected. Quotes are reconstructed from retrieved sections; semantic support still needs review. The model sees fictional document data, not visitor credentials. The [Ollama API](https://docs.ollama.com/capabilities/tool-calling) supplies native tool calls and [schema-constrained output](https://docs.ollama.com/capabilities/structured-outputs).

The 9 October 2026 100-ad sample counted C#/.NET in 20, React in 26, APIs in 51, SQL/relational databases in 42, testing in 64 and CI/CD in 58. This dated convenience sample supports the stack, not a market-wide demand estimate. [Scope/research](PLAN.md).

## Start locally

Prerequisites: Node.js 24/npm, .NET SDK from global.json (repo-local SDK used when present), PowerShell 7, Docker and Ollama with qwen3.8:27b installed. No hosting/billing account.

From repository root:

```powershell
npm ci
pwsh -NoProfile -File scripts/intake-local.ps1 setup
```

Start Ollama in a separate terminal if it is not running:

```powershell
$env:OLLAMA_HOST='127.0.0.1:11434'
$env:OLLAMA_NO_CLOUD='1'
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" serve
```

Start API and interface in separate terminals:

```powershell
pwsh -NoProfile -File scripts/intake-local.ps1 api
```

```powershell
node node_modules/vite/bin/vite.js --config preview/content-snare/vite.config.ts
```

Open **http://127.0.0.1:5622/l/instructions**. Vite proxies the same-origin API to loopback 5080; C# calls loopback Ollama on 11434. Keep all three processes running. Cold model loading is slower than warm inference; unavailability preserves confirmed drafts. Load saved work after uncertainty, rather than repeating the action.

## Checks and evaluation

```powershell
pwsh -NoProfile -File scripts/intake-local.ps1 test
node node_modules/typescript/bin/tsc -p preview/content-snare/tsconfig.json --noEmit
node node_modules/vite/bin/vite.js build --config preview/content-snare/vite.config.ts
```

On Windows, stop the API with Ctrl+C before rebuilding or running .NET tests; the running process locks its assemblies. Restart it afterwards. The .NET suite uses deterministic doubles and disposable local databases. Opt-in evaluation calls the actual model for complete, missing/resumed and conflicting-source cases; every attempt is kept in a unique ignored evidence directory:

```powershell
pwsh -NoProfile -File scripts/intake-local.ps1 evaluate
node node_modules/@playwright/test/cli.js test --config preview/content-snare/playwright.config.ts
```

The separate browser suite uses this local API/model/UI and requires all three processes. Existing CI does not run live inference. Output stays in preview/content-snare/dist/; root npm run build still targets the earlier app. No deployment is part of these commands.

## Source and plan

| Path                                        | Responsibility                                                                          |
| ------------------------------------------- | --------------------------------------------------------------------------------------- |
| src/OnboardFlow.Api/ClientRequests          | Fixed fictional schema/sources, rules/store, model adapter, agent and endpoints.        |
| preview/content-snare/src                   | Reference UI and typed same-origin client; no canned extraction or browser persistence. |
| tests/OnboardFlow.Api.Tests                 | Deterministic API/rule/security checks and opt-in real-model evaluation.                |
| [PLAN.md](PLAN.md)                          | CS-01–CS-07 scope and limits.                                                           |
| [Architecture](docs/architecture.md)        | Integration and SOLID boundaries.                                                       |
| [Walkthrough](docs/employer-walkthrough.md) | Visitor/reference contract.                                                             |
| [HARNESS.md](HARNESS.md)                    | Commands, actual results, failed attempts and limits.                                   |
| [AGENTS.md](AGENTS.md)                      | AI development guardrails.                                                              |

Local implementation only: Production does not map the new intake endpoints. Public inference is unconfigured; live app/website/hosting/CI release settings are unchanged. A release needs a separate instruction.

## Earlier application baseline

[Published app](https://dexter-onboardflow.de.deplexo.com/) is the earlier manager/website-scope/simulated-handoff version. It has neither this interface nor AI agent. This local slice adds C# intake code and an additive local migration, while its deployed version, legacy frontend, hosting and delivery remain unchanged.

[HARNESS](HARNESS.md) retains setup/evidence; [case study](docs/case-study.md) describes earlier version. Original commands: npm run setup, npm run build, npm run api, npm run verify (.NET SDK from global.json, PowerShell 7, Docker). Old test counts/deployed results do not prove agent behaviour.

Zero additional cost/no card remain constraints. Public hosted inference is unverified; local Ollama is verified. No paid fallback or unlimited-public-runs promise.

[MIT licence](LICENSE). Dependencies retain own licences; [third-party notices](THIRD_PARTY_NOTICES.md).
