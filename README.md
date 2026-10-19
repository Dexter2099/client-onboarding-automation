# OnboardFlow

A client-onboarding workspace built with **React, TypeScript, C#, ASP.NET Core and PostgreSQL**. Review a signed website project's client answers, resolve an out-of-scope request and save an approved kickoff brief with a separately confirmed simulated handoff.

**Version-one review package · 19 October 2026.** The combined React/C# app is deployed on Deplexo Free with Neon Free PostgreSQL 17. Secure workspace creation and the bounded hosted workflow/recovery/security checks pass. Extended-idle entry, first-time human timing and employer-access reputation remain open, so employer sharing is still behind review. The planned entry is a Try this demo button beside GitHub on the OnboardFlow card at dexter-developer.dev, opening the working app separately. [Hosting evidence](HARNESS.md) distinguishes observed passes from remaining acceptance. All client data is fictional; email delivery and the external register connection are simulated.

## Try the demo

Open the [hosted OnboardFlow demo](https://dexter-onboardflow.de.deplexo.com/). You're the project manager for Juniper Studio, reviewing client replies and preparing its signed website redesign for kickoff. No account or personal information is needed.

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

The harness builds the application, runs PostgreSQL-backed API checks and exercises the built UI in Chromium. API and browser checks own disposable local test databases; they do not reset the development database. Current evidence is **50 API/security tests and 11 browser tests passing**, including seventeen Production HTTPS/proxy cases and API resource/error controls. See [HARNESS.md](HARNESS.md) for the commands, evidence and practical limits.

Stop the API/Vite terminals with Ctrl+C. Stop PostgreSQL while retaining its volume with:

```powershell
npm run stop:db
```

Run `npm run setup` before the next start. If setup fails, check Docker is running Linux containers and `dotnet --version` resolves the pinned SDK from this folder. Use a fresh terminal if an inherited `ConnectionStrings__OnboardFlow` override points the API elsewhere. Do not delete local credentials to troubleshoot a retained database.

## Container package

The Dockerfile builds the React frontend and C# API from source, then runs the published application as a non-root user on port 8080. Build it from the repository root:

```powershell
docker build --tag onboardflow:review .
```

Only the final runtime image is deployed; Node, npm, the .NET SDK and tests stay in build stages. The build context excludes local credentials, database state and generated files. deplexo.yaml configures a single web container on port 8080. The container disables JSON config reload (`DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`) because the shared host exhausted its file-watcher allowance during the first startup. Apply configuration changes by restarting/redeploying the app.

At runtime, supply `ConnectionStrings__OnboardFlow` through the host's secret environment settings, using a PostgreSQL connection with verified TLS. Do not commit it or bake it into an image. Apply the checked-in EF migrations to the intended database before serving visitors; image startup does not migrate it. Keep Production enabled, retain a writable persistent /data mount for ASP.NET keys, and configure `ReverseProxy__KnownProxies__0` (and further indexes if needed) only for verified proxy addresses. HTTPS and Secure-cookie/CSRF behaviour must pass the hosted checks before release. Do not trust arbitrary forwarders to make deployment pass.

The verified Deplexo ingress replaces `X-Forwarded-Proto` with its internal HTTP scheme but preserves Cloudflare's documented `CF-Visitor` scheme. Set `ReverseProxy__UseCloudflareVisitor=true` only for that verified path, alongside its exact configured peer. This opt-in adapter discards any visitor-supplied derived header, accepts a single valid HTTP/HTTPS scheme only from an explicitly configured proxy, and passes it through ASP.NET's forwarded-header middleware. It fails closed when that evidence is missing or invalid. Keep the option off for other hosts unless their ingress and header-overwrite behaviour have been verified. See [Cloudflare's header reference](https://developers.cloudflare.com/fundamentals/reference/http-headers/#cf-visitor) and the retained hosted checks in HARNESS.md.

The built image passed the focused local Production/resource probe documented in HARNESS.md. This does not establish provider allocation, browser performance or long-idle acceptance.

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

The focused quality review separates HTTP endpoints, workflow transactions, pure validation and response mapping. The API enforces ownership, fixed expiry, antiforgery, revision checks and repeat/attempt limits on the server. It also limits body size and API traffic, returns controlled errors and avoids caching private API responses. [Architecture and API review](docs/architecture.md) maps these controls to relevant OWASP risks without claiming certification.

## Continuous integration and deployment

[![Delivery](https://github.com/Dexter2099/client-onboarding-automation/actions/workflows/delivery.yml/badge.svg)](https://github.com/Dexter2099/client-onboarding-automation/actions/workflows/delivery.yml)

The [Delivery workflow](.github/workflows/delivery.yml) runs on pull requests and main, with a manual trigger available. It installs the locked dependencies, starts disposable local PostgreSQL, checks formatting, runs all API/browser tests, packages the exact checked-out commit and builds that package's Docker image. A failing check prevents deployment. Test reports and the verified source package are retained for seven days.

Only a successful main run can access the **production** deployment secret. It downloads that run's verified package, checks its SHA-256 and revision, uploads it to the existing Deplexo app and waits for that exact deployment to succeed. The live smoke check verifies `/release.json` matches the Git commit, then checks real database-backed workspace creation, Secure cookies, CSRF, fixed expiry and saved/repeated Start. A failed smoke check fails the release job; it does not blindly repeat a mutation or claim success. Production runs are serial and are not automatically cancelled during a container swap.

Deplexo's existing ZIP app has no supported in-place Git-source conversion. GitHub Actions therefore delivers a versioned source archive through the documented API; there is no manual ZIP upload in the normal release path. GitHub's run/deployment record, package checksum and live revision marker tie it to the source commit. Deplexo rebuilds the same package; CI does not publish a prebuilt image. Base images are pinned by digest, actions by commit SHA, and npm/NuGet inputs by checked-in lockfiles with locked restore. This makes inputs repeatable without claiming bit-for-bit identical image output. Review dependency/base-image updates deliberately; regenerate NuGet locks with `dotnet restore OnboardFlow.slnx --use-lock-file --force-evaluate` when changing package versions, then run the harness.

The production environment requires one `DEPLEXO_TOKEN` secret with **app:deploy, app:restart and app:read** only. It does not need database credentials, environment-variable access, deletion, stop, domains or logs permissions. The token is account-scoped because Deplexo does not offer a per-app scope; the script additionally fixes the existing app ID. Dexter approved **No expiry** on 19 October 2026 to avoid scheduled credential replacement. Keep it out of files/logs and revoke/replace it if exposed or no longer needed. The Neon secret remains solely in Deplexo. Provider push auto-deploy stays disabled so it cannot bypass the checks.

This key is used only for future deployments. Revoking or expiring it prevents later automated releases; it does not stop the already-running app or expire visitor access. The fixed 24-hour visitor sandbox lifetime is a separate application rule.

Main requires the **Verify and package** check from the GitHub Actions app, up to date with the target branch; administrators are included. Force pushes and branch deletion are disabled. The production environment permits only main. The first [PR verification run](https://github.com/Dexter2099/client-onboarding-automation/actions/runs/39186999599) passed 50 API tests, eleven browser tests and the packaged image build.

To release, merge a passing pull request into main. To rerun main deliberately, use Delivery's **Run workflow** button. Inspect a failed deployment/history before retrying if submission or confirmation was lost. There is no automatic rollback: source rollback cannot undo database changes or restore provider key files. This slice makes no schema changes. The host can briefly interrupt service while replacing its container. See [HARNESS.md](HARNESS.md) for actual execution evidence and limits.

First-time human walkthrough timing and ongoing-free hosting/startup acceptance remain open. This is a personal portfolio project, not production client work. There is no real email sending or Make/Google Sheets integration in version one. Local automated timing is not a measured business saving or a human usability result.

Application code is [MIT licensed](LICENSE); dependencies retain their own licences. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
