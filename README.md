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

## How it works

`IntakeAgentService` coordinates two read-only tools through the small `IIntakeModelClient` interface. `IntakeRules` validates each draft and its source before `IntakeStore` saves it in PostgreSQL. Editing an answer clears its source attribution and requires resubmission. React components separate navigation, field rendering, source inspection and media from application state.

The hosted adapter uses a pinned, evaluated OpenAI model. Each sandbox permits three runs, with six model calls per run including continuation, 512 output tokens per call and a 15-second call timeout. There is no automatic retry or provider fallback.

The dedicated OpenAI project has a US$5 monthly limit. `IntakeSpendBudget` reserves costs in a shared PostgreSQL ledger and stops new inference at US$4.50. Uncertain calls retain their reservations; resets and restarts cannot erase spending. Manual completion remains available when inference is unavailable.

Owned 24-hour sandboxes, secure cookies, CSRF protection, source validation and command/version checks protect saved work. GitHub Actions verifies application changes before publishing the checked source to Hostless. Documentation changes receive link and formatting checks without deploying the app.

The public demo uses a [signed Cloudflare gateway](edge/README.md), so changing hosting proxy addresses cannot cause the earlier HTTPS self-redirect loop. An hourly availability check tests page access, protected sessions and database readiness without AI calls. Current live acceptance belongs in the [release evidence](HARNESS.md).

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
