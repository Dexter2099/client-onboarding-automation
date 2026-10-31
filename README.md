# OnboardFlow

An AI onboarding app that drafts answers from fictional client documents, shows supporting passages and asks for missing details before human submission.

React/TypeScript, C# / ASP.NET Core / .NET 10, PostgreSQL and server-side OpenAI.

[Try the demo](https://dexter-onboardflow.pages.dev/)

## Try it

1. Select Let's get started, then Fill with AI.
2. Open View source to check a draft against its supporting passage.
3. Supply the labelled sample reply for the missing fictional tax reference, then continue filling.
4. Complete the four manual sample tasks and submit the answers or page for human review.

The request has 11 information fields and 12 task items. Submitted items await review; the AI cannot submit, approve or perform external actions. The demo uses fictional data and sample attachments. Refresh restores saved progress. The Instructions video is labelled as the earlier local Ollama recording.

**Reset demo** is available throughout the request, including during AI filling, with confirmation before clearing answers, questions and submissions. Reset preserves the visitor's lifetime and AI usage limits. The mobile header keeps the control reachable while scrolling.

## How it works

`IntakeAgentService` coordinates two read-only tools through the small `IIntakeModelClient` interface. `IntakeRules` validates each draft and its source before `IntakeStore` saves it in PostgreSQL. Editing an answer clears its source attribution and requires resubmission. React components separate navigation, field rendering, source inspection and media from application state.

The client combines adjacent unsent edits to the same field, preserving field/action ordering and saving the latest value before submission. Startup fetches CSRF protection and the owned intake together, without a separate session lookup. Creating a visitor returns the initial request in the same transaction and seeds no older-demo projects. Ordinary intake reads fetch ownership and request state in one unlocked query; initialization, source changes and expired runs retain their locked transitions. Unchanged polling responses do not repeat navigation updates. Existing stored project records remain supported without being created for new visitors.

Ordinary reads and saves have a 15-second client deadline. After a failure, **Load saved work** discards queued edits and late responses, reloads persisted work and allows further saves; it never automatically replays an uncertain write. Recovery reuses the two parallel startup reads. Saves retain their owned transaction and locks while avoiding redundant nested savepoint/release operations. Completed model conversations are trimmed on mutation while answers, sources, trace, command receipts and usage records remain intact. Numeric `Server-Timing` headers distinguish application time from the public round trip. The superseded frontend and its exclusive Inter dependency have been retired.

The hosted adapter uses a pinned, evaluated OpenAI model. Each sandbox permits three runs, with six model calls per run including continuation, 512 output tokens per call and a 15-second call timeout. There is no automatic retry or provider fallback.

The dedicated OpenAI project has a US$5 monthly limit. `IntakeSpendBudget` reserves costs in a shared PostgreSQL ledger and stops new inference at US$4.50. Uncertain calls retain their reservations; resets and restarts cannot erase spending. Manual completion remains available when inference is unavailable.

Public sandboxes allow three AI runs. Dexter can activate a privately signed permanent owner pass at `/api/owner-access` to bypass only that sandbox run count. The signed grant has no application expiry. Its Secure/HttpOnly/host-only cookie persists across resets/new sandboxes and renews for up to 400 days on existing CSRF startup and private status reads, within Chrome's cookie limit. Clearing/evicting browser cookies or not using the demo for longer than that can require activation again. Existing bounded 90-day passes remain supported until their own expiry. The monthly spending cap, per-run limits, ownership and manual-only actions still apply. Public demo screens/navigation are unchanged.

The app contains only the separate owner public verification key. Keep its private signing key encrypted with Windows DPAPI under `.local/owner-access/`, separate from the gateway key. `pwsh -NoProfile -File scripts/create-owner-pass.ps1 -Permanent` issues a non-expiring pass into an encrypted local file; omitting `-Permanent` retains bounded issuance. It never prints the pass. Activate privately through the password form, never through a URL, chat, source control or a log. Issuing a new pass needs no application deployment. A pass is a bearer credential: disabling access removes only that browser's cookie, whereas replacing the owner public key revokes all previous passes without changing gateway keys. Never share the pass; its authority is limited to the sandbox run-count exemption.

Owned 24-hour sandboxes, secure cookies, CSRF protection, source validation and command/version checks protect saved work. GitHub Actions verifies application changes before publishing the checked source to Hostless. Documentation changes receive link and formatting checks without deploying the app.

Deterministic API and browser regression runs use a test-only scripted adapter, with no inference. Real-AI macOS acceptance additionally requires the explicit `LIVE_AI_ACCEPTANCE_ENABLED=true` repository variable; leave it unset for zero-AI releases. Connected Chrome live acceptance uses manual actions with the AI endpoint blocked. The evaluated model, prompt and inference/spending limits are unchanged by the latency and cleanup release.

The public demo uses a [signed Cloudflare gateway](edge/README.md), so changing hosting proxy addresses cannot cause the earlier HTTPS self-redirect loop. Hostless checks HTTP `/health` every 10 seconds for process recovery. One free UptimeRobot monitor checks the actual demo URL every five minutes, follows redirects and sends DOWN/RECOVERY emails. Technical incidents stay in the hosting dashboard. Current release verification belongs in the [release evidence](HARNESS.md).

## Run locally

Install Node.js 24/npm, the .NET SDK in `global.json`, PowerShell 7, Docker and Ollama with `qwen3.8:27b`.

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

Open http://127.0.0.1:5622/l/instructions. Keep Ollama on loopback with `OLLAMA_HOST=127.0.0.1:11434` and `OLLAMA_NO_CLOUD=1`. Local development uses Ollama; production credentials stay in server settings.

## Build and check

```powershell
npm run build
npm run format:check
npm run verify
```

Ordinary regression uses a test-assembly model double. Real-model evaluations and hosted acceptance are separate, explicitly run checks.

## Further reading

- [Architecture and deployment](docs/architecture.md): component boundaries, free Hostless allocation, server configuration and rollback.
- [Walkthrough](docs/employer-walkthrough.md): the visitor journey.
- [Test and release evidence](HARNESS.md): checked revisions, evaluation results, timings and limitations, including unmeasured long-idle performance.
- [Case study](docs/case-study.md): the problem and engineering decisions.

The interface follows [Content Snare's demo](https://demo.contentsnare.com/shared/21481036-b8ca-4abb-ac0b-4d09b663333a/l/instructions).

[MIT licence](LICENSE) · [Third-party notices](THIRD_PARTY_NOTICES.md).
