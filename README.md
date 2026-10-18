# OnboardFlow

A client-onboarding workspace built with **React, TypeScript, C#, ASP.NET Core and PostgreSQL**. Review a signed website project's client answers, resolve an out-of-scope request and save an approved kickoff brief with a separately confirmed simulated handoff.

**Version-one review package · 19 October 2026.** The application runs locally. Public hosting and deployment have not been validated or performed. All client data is fictional; email delivery and the external register connection are simulated.

## Try the demo

You play Alex, the project manager for Juniper Studio. No account or personal information is needed.

1. Open OF-1042 and select **Start onboarding**, then **Load Maya's sample reply**.
2. Approve Goals & audience and follow **Review Pages**. Shop is outside the signed five-page scope, so request a correction with a comment.
3. Load the corrected Pages, approve that revision, then review and approve Brand assets.
4. Select **Mark ready for kickoff**, inspect the approved brief and **Publish simulated handoff**.

OF-1043 is the optional recovery example: its first welcome attempt fails. Restore the simulated service and explicitly try again. There are **three attempts total per project**, including the first; restoring availability does not reset the budget.

Work survives refresh in a private sandbox that expires 24 hours after creation. **Reset both sample projects** clears only your visitor's two projects. Outbox, Activity, earlier revisions and register details make the saved results inspectable.

## Run locally

Verified on Windows with PowerShell 7. Install these prerequisites first:

- Node.js 24 and npm (verified with npm 11).
- .NET SDK **10.0.402**, or the compatible patch selected by [global.json](global.json). The SDK is not bundled in this repository.
- Docker Desktop running Linux containers, with Docker Compose available.
- Git, to obtain the source.

From the repository root, in **PowerShell 7**, run:

```powershell
npm ci
npm run setup
npm run build
npm run api
```

Open **http://127.0.0.1:5080/**. Use `127.0.0.1` consistently so the sandbox cookie stays with the same hostname.

`setup` creates an ignored `.local/database.env` with a random local password, starts PostgreSQL, restores the .NET solution and EF tool, applies migrations and installs Playwright Chromium. No provider account, webhook or manually supplied database secret is needed. Initial setup requires internet access for packages, the database image and browser download.

The development database uses `127.0.0.1:54329`. The app uses port 5080; browser checks use 5081. Keep these ports available. Setup preserves existing database data: keep `.local/database.env` when retaining the Docker database volume. Use one local checkout/container arrangement at a time.

For frontend editing, keep `npm run api` running and open another PowerShell terminal:

```powershell
npm run dev
```

Vite runs at **http://127.0.0.1:5173** and proxies `/api` to the C# application. The API serves the last frontend build on port 5080; rebuild after edits when reviewing that port.

## Verify and stop

```powershell
npm run verify
npm run format:check
```

The harness builds the application, runs PostgreSQL-backed API checks and exercises the built UI in Chromium. API and browser checks own disposable local test databases; they do not reset the development database. Current evidence is **30 API/database tests and 11 browser tests passing**. See [HARNESS.md](HARNESS.md) for the commands, evidence and practical limits.

Stop the API/Vite terminals with Ctrl+C. Stop PostgreSQL while retaining its volume with:

```powershell
npm run stop:db
```

Run `npm run setup` before the next start. If setup fails, check Docker is running Linux containers and `dotnet --version` resolves the pinned SDK from this folder. Use a fresh terminal if an inherited `ConnectionStrings__OnboardFlow` override points the API elsewhere. Do not delete local credentials to troubleshoot a retained database.

## Project structure and evidence

| Path | Purpose |
| --- | --- |
| `src/web` | React interface and typed API calls. |
| `src/OnboardFlow.Api` | Same-origin C# API, EF model, migrations and static frontend host. |
| `tests` | PostgreSQL-backed API tests and browser journeys. |
| [Case study](docs/case-study.md) | Short employer-facing draft describing the problem, choices and evidence. |
| [Recording script](docs/demo-recording-script.md) | Draft main walkthrough and optional recovery segment; no recording has been produced. |
| [PLAN.md](PLAN.md) | Scope and acceptance criteria. |
| [HARNESS.md](HARNESS.md) | Reproducible checks and current review/release gates. |

This improves an earlier prototype; its assessment and reuse decisions are in [docs/architecture.md](docs/architecture.md). The current implementation uses one C# app, direct EF Core and PostgreSQL transactions, with no background job or generic workflow engine.

First-time human walkthrough timing and ongoing-free hosting/startup acceptance remain open. This is a personal portfolio project, not production client work. There is no real email sending or Make/Google Sheets integration in version one. Local automated timing is not a measured business saving or a human usability result.

Application code is [MIT licensed](LICENSE); dependencies retain their own licences. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
