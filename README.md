# OnboardFlow

A full-stack client-onboarding demo built with **React, TypeScript, C#, ASP.NET Core and PostgreSQL**. A project manager reviews a signed website project's client replies, resolves an out-of-scope request and saves an approved kickoff brief with an inspectable handoff.

[Try the live demo](https://dexter-onboardflow.de.deplexo.com/) · [Engineering case study](docs/case-study.md) · [QA evidence](docs/case-study.md#qa-and-testing-evidence)

[![Delivery](https://github.com/Dexter2099/client-onboarding-automation/actions/workflows/delivery.yml/badge.svg)](https://github.com/Dexter2099/client-onboarding-automation/actions/workflows/delivery.yml)

## The problem

Receiving client information does not mean it is ready for kickoff. A reply may add work outside the signed agreement, and a correction must receive fresh approval. Repeated clicks or a lost response must not duplicate requests, messages or handoffs.

The fictional Juniper Studio project makes those rules visible. Maya's first reply adds Shop to a signed five-page website. The visitor acts as the project manager, requests a correction and approves the revised answer before marking the project ready.

## Try it

No account, installation or personal information is needed. Use a current browser with JavaScript and same-origin cookies enabled.

1. The main walkthrough opens directly on OF-1042. Select **Start onboarding**, then **Load Maya's sample reply**.
2. Approve Goals & audience. Review Pages and request a correction with a comment because Shop is outside the agreement.
3. Load the corrected Pages, approve that revision, then approve Brand assets.
4. **Mark ready for kickoff**, inspect the saved brief and **Publish simulated handoff**. Inspect its register entry and repeat Publish to see the same result reused.

Below the main walkthrough, **Try failure recovery** shows how the app handles a service failure without losing completed work. It uses the separate OF-1043 fixture:

1. **Start onboarding** saves the request and staff review task, but deliberately fails to prepare the welcome message because its simulated service is unavailable.
2. **Restore simulated service** pretends the service is available again. It does not retry anything or reset the attempt counter.
3. **Try welcome** retries only the failed welcome step. The guided example succeeds on attempt two, keeping the original request and task.

This demonstrates preserved data, controlled retries and duplicate prevention. No real service is contacted or email sent. There are three welcome attempts total per project. **Back to main walkthrough** returns to your unchanged OF-1042 work. Refresh opens the main walkthrough; both projects' saved state remains available.

Each visitor has a private fictional sandbox with a fixed 24-hour lifetime. Work survives refresh. **Reset both sample projects** replaces only that visitor's projects.

The public demo link also works on another laptop or network; no local server, shared Wi-Fi, firewall rule or installation is needed. Each browser/device receives its own sandbox, so work is not transferred between devices.

## What is real and what is simulated?

| Part | What actually happens |
| --- | --- |
| Review and approval | The C# API enforces signed scope, current-revision approval and readiness. PostgreSQL stores the answers, decisions, history and frozen kickoff brief. |
| Visitor workspace | Server-enforced ownership and expiry isolate visitors. Reset, persistence and repeat/concurrency protection use the real API/database. |
| Client replies | Labelled demo controls supply prepared fictional replies. There is no separate client portal. |
| Email / Outbox | The app saves prepared welcome/correction messages for inspection. Delivery is simulated: no email is sent and visitors supply no email address. |
| Handoff | Publish stores a real snapshot of the approved brief in the visitor's demo register. Repeats reuse that saved entry. The external destination is simulated: no Make, Google Sheets or other external register is updated. |

This is a personal portfolio project with fictional data. Real external delivery/integration is outside version one.

## Engineering and QA

- **Frontend:** React/TypeScript, contextual next actions, keyboard controls and desktop/phone layouts.
- **Backend:** one same-origin ASP.NET Core app on .NET 10 LTS, EF Core/Npgsql and PostgreSQL 17. Direct transactions and database constraints protect workflow consistency.
- **Reliability:** revision-bound approvals, idempotent commands, explicit lost-response recovery, rollback checks and a three-attempt welcome budget.
- **Security:** private expiring sandboxes, ownership checks, Secure/HttpOnly cookies, CSRF protection, resource limits, controlled errors, enforced CSP and HTTPS HSTS. These are OWASP-aware controls, not a certification.
- **Delivery:** Docker on Deplexo Free with Neon Free; passing GitHub Actions checks gate deployment and verify the live Git revision.

| Automated check | Coverage |
| --- | --- |
| Unit | 16 isolated scope/readiness cases without an API host or database. |
| Integration / security | 57 ASP.NET pipeline and PostgreSQL cases, including ownership, expiry, stale approvals, concurrency and injected failures. |
| Browser E2E | Twelve scenarios in Chromium, Firefox and WebKit: 36 executions covering the main journey, recovery, persistence, isolation and CSP. |
| Post-deploy smoke | Live revision/API/security checks followed by a native macOS arm64/WebKit visitor journey at desktop/phone widths and 2x scale. |

Regression, security and recovery describe overlapping test purposes; they are not additional counts. See the [QA table](docs/case-study.md#qa-and-testing-evidence) for source links, failure cases and limits, and the [delivery history](https://github.com/Dexter2099/client-onboarding-automation/actions/workflows/delivery.yml?query=branch%3Amain) for actual release results. Detailed CI reports/screenshots are retained for seven days.

## Run locally

Prerequisites: Node.js 24/npm, .NET SDK **10.0.402** selected by [global.json](global.json), PowerShell 7 and Docker with Linux containers. Local setup is verified on Windows; CI runs the harness on Ubuntu.

```powershell
npm ci
npm run setup
npm run build
npm run api
```

Open **http://127.0.0.1:5080/**. Setup creates ignored local database credentials, starts PostgreSQL, applies migrations and installs the three Playwright engines. It preserves existing development data and requires no hosting account. Keep `.local/database.env` when retaining the database volume.

For frontend development, run `npm run dev` in another terminal and open http://127.0.0.1:5173/. Vite proxies the C# API. The API on port 5080 serves the last frontend build. PostgreSQL uses port 54329; browser checks use 5081.

```powershell
npm run verify
npm run format:check
```

The full harness builds the app and runs unit, integration and browser checks. Database tests own disposable local fixtures and never reset development or hosted data. Unit cases can run alone with `dotnet test tests/OnboardFlow.Api.Tests --filter "Category=Unit"`.

Stop API/Vite with Ctrl+C; `npm run stop:db` stops PostgreSQL while retaining its volume. Run `npm run setup` before the next start. See [HARNESS.md](HARNESS.md) for troubleshooting and exact verification evidence.

## Code and deployment

| Path | Responsibility |
| --- | --- |
| `src/web` | React workspace, typed API client and onboarding UI. |
| `src/OnboardFlow.Api` | HTTP endpoints, workflow transactions, pure rules, response mapping, EF model/migrations and static hosting. |
| `tests` | Isolated rule tests, API/database integration checks and Playwright journeys. |
| `.github/workflows/delivery.yml` | Required PR checks, source packaging/image build, production deployment and live smoke verification. |

Merge a passing PR into main to release. Actions packages the checked commit, verifies its checksum and sends that source archive to the existing Deplexo app, which rebuilds it. This is verified source delivery, not promotion of an identical prebuilt image. Main releases are serial; production credentials stay in the production environment, and database credentials stay with the host. A failed live check fails release verification; mutations are not blindly retried.

Implementation and production configuration are described in [architecture.md](docs/architecture.md). [HARNESS.md](HARNESS.md) owns operational/release evidence; [PLAN.md](PLAN.md) owns scope and acceptance criteria. A [recording script](docs/demo-recording-script.md) is available; no completed recording is claimed.

## Known limits

Neon Free suspends database compute after five idle minutes and wakes on access. Post-idle requests can take longer; [HARNESS.md](HARNESS.md) distinguishes measured suspended-database entry from warm timing. No keep-alive workaround, always-on guarantee or sustained-load result is claimed.

Branded Safari, physical phones, screen readers and a full accessibility audit remain unverified. Human first-time usability testing is separate from automated browser acceptance. A Webroot review was submitted; vendor clearance remains unconfirmed. Visitors should not bypass a security warning.

Application code is [MIT licensed](LICENSE); dependencies retain their own licences. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
