# OnboardFlow agent instructions

## Current boundary

- **Version-one source/review package prepared on 19 October 2026; stop for review.** Dexter authorised a local source commit, README setup instructions, a short case-study draft and a recording-script draft. No push, public release or deployment is authorised. HARNESS.md owns remaining human timing, hosting/account/startup, actual recording and publication gates. Do not label AC-10 complete from automated evidence or add feature/architecture scope.
- **Planning stop: CP-1 is complete.** General planning is closed. Do not restart planning or completed milestones without a concrete reason. Resolve technical details within the next authorised slice. Reopen planning only for a concrete material blocker or Dexter's scope change. Keep the existing five documents.
- This is improvement of an existing prototype. Preserve and assess it before deciding what to retain, repair, replace or retire. Do not assume a rewrite or blindly preserve obsolete behaviour.
- Canonical workspace: `C:\Users\User\Documents\Project_Directories2\OnboardFlow`.
- The legacy snapshot is local tag `codex/legacy-prototype-2026-10-19`, tree `01505e94b0c309ba8138dffe8bb4ce7846ab1d54`. It is not a checked-out branch. Do not delete it or run repository cleanup that could discard the baseline.

## Read and scope

- Read [PLAN.md](PLAN.md) for scope, acceptance IDs and current decisions. Then read only the relevant [walkthrough](docs/employer-walkthrough.md), [architecture](docs/architecture.md) or [harness](HARNESS.md) section.
- Direct user instructions take precedence. Surface a material conflict rather than silently changing documents to justify added work.
- The accepted approach is one same-origin ASP.NET Core application serving React/TypeScript and its API, using .NET 10 LTS, compatible EF Core/Npgsql and PostgreSQL. Hosting is unresolved and does not block a local foundation. Follow the architecture's working data/state design; do not substitute old Workers/D1 checks as target-stack evidence.
- Dexter approved the four version-one boundaries on 19 October 2026: prepared signed projects, Alex as the sole visitor role with sample client replies, correction-only handling of Shop, and repeated Start/Publish plus one bounded welcome-message recovery scenario. No client/project creation or editing flow, client portal, pricing/contract variation, reminders or extra scenarios. New features belong in PLAN.md's backlog until Dexter agrees to scope them.
- Version one uses an application-owned, clearly labelled simulated handoff. Dexter deferred the real Make/Sheets integration beyond the first release on 19 October 2026; do not reintroduce it as a version-one dependency or let visitors trigger real connected writes.
- Portfolio-site edits and publication belong to the other chat.
- README.md, docs/case-study.md and docs/demo-recording-script.md are employer/setup deliverables, not additional plans. Keep scope and verification authority in the existing five working documents. Draft narration must describe prepared/simulated results accurately and must not claim a completed recording, deployment or human timing result.

## Execution and verification

- Once implementation is authorised, follow HARNESS.md: one bounded slice, relevant acceptance checks, diff review and a concise evidence-based checkpoint.
- Use exact verified commands from HARNESS.md: `npm ci`, `npm run setup`, `npm run api`, `npm run dev` and `npm run verify`. Setup requires .NET 10, PowerShell 7, Node/npm and Docker with Linux containers. Do not substitute legacy scripts or tests as target-stack evidence.
- Preserve unrelated user changes. Avoid broad refactors, extra infrastructure, speculative abstractions and dependency churn outside the slice.
- Dexter reiterated avoiding over-engineering on 19 October 2026. Keep one C# app, one test project and direct EF Core access. Nine business tables now include WelcomeAttempt and RegisterEntry. No generic jobs, workflow configuration, extra infrastructure or new dependencies are needed.
- Enforce scope, approval, sandbox ownership and repeat safety on the server/database. Disabled buttons and client-provided identifiers do not enforce those rules.
- Keep submitted revisions distinct from approvals; a changed answer needs fresh review. Mark ready stores the approved brief and freezes replies/reviews. Readiness and handoff confirmation are separate.
- Welcome recovery allows three committed attempts total per project, including the first. The additive migration counts existing successful welcomes as attempt one. Preserve completed setup, keep restoration separate from budget and never add automatic retries. Replaying the same command cannot consume another attempt; a lost response requires reading saved state before a new command.
- Dexter requested a simpler employer experience and larger text on 19 October 2026. Preserve the implemented UI contract: 16px body/action text, at least 14px supporting text, contextual next actions and secondary detail disclosed on demand. Keep scope blockers, simulation labels and reset consequences visible. No business features or design system were added.
- Enforce fixed server-side expiry 24 hours after sandbox creation; activity never extends it. Reset replaces both projects for only that visitor. Expired/reset credentials fail even before physical cleanup. A fresh start must never replay an expired mutation automatically.
- Tests use disposable fictional fixtures, never a shared remote or development database. Retain meaningful outcomes, including negative and recovery cases, rather than tests that merely mirror implementation.
- Label simulation, recording and real connection accurately. A screenshot or mockup is not proof of API/database behaviour. Report checks actually run and known gaps.

## Cost and data

- Zero additional cost: no paid plans, purchased infrastructure, temporary-credit dependencies or paid fallback. Verify actual hosting eligibility and limits before deployment.
- Dexter ruled out a minute-long startup wait on 19 October 2026. Render Free is excluded for this demo; do not evade the requirement by rendering a shell while leaving the workflow blocked. Hosting remains unselected.
- Do not send real email, request visitor personal data or expose provider credentials. Keep demo controls separate from ordinary project actions.
- Never commit secrets, local database state or invented billing/test/deployment evidence. Local success does not establish live acceptance; deployment requires Dexter's instruction after review.

## PMB project memory

Follow the connection's supplied managed PMB instructions. Before substantive work, call `prepare` with the user's message and read the surfaced lessons. Apply relevant lessons and mark them with an explanatory note; mark unrelated lessons not applicable.

At the start of a non-trivial task, recall relevant lessons/failures. Before a new tool, command or pattern, check `find_lessons` and `recall`; follow settled decisions unless concrete new evidence warrants a change. Log substantive completions, decisions, corrections and failures in one `record_batch` per turn, scoped to `OnboardFlow`. Record future intent as a goal, not a completed fact; do not record secrets or transient listings. Use absolute dates and `session_brief` to recover context after compaction. Do not recall after writing merely to verify the write.

Reference: [OpenAI's AGENTS.md guidance](https://learn.chatgpt.com/docs/agent-configuration/agents-md).
