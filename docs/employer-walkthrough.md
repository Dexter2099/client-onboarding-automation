# Employer walkthrough and project screen

Updated 19 October 2026. **Local review and the bounded hosted workflow/security checks pass; publication remains behind review.** The Deplexo/Neon workspace failure is resolved. [PLAN.md](../PLAN.md) owns scope; [HARNESS.md](../HARNESS.md) records evidence/gates. The [case study](case-study.md) and [recording script](demo-recording-script.md) are drafts. Extended-idle entry, first-time human timing and an actual recording remain pending. The portfolio entry is unchanged.

The application implements the main review/correction/brief journey, repeat protection, OF-1043 welcome recovery and the persisted simulated register. The UI now uses larger text, shorter instructions and contextual actions. Refresh retains saved work. Readiness initially shows Handoff: not published; explicit Publish stores a separate confirmation. Stop for review at this checkpoint.

## Entry and main journey

The public entry agreed on 19 October 2026 is an OnboardFlow project card on **dexter-developer.dev**, with **Try this demo** beside **GitHub**. Try this demo opens the separately hosted application in a new tab/window and lands directly on the prepared project screen below. The portfolio remains open. No iframe, extra portal or purchased custom domain is required. The button must target a verified public HTTPS app URL; localhost is only for development. Portfolio-site implementation/publication remains separate work.

Open directly on the prepared Juniper Studio project. The introduction says “You're the project manager, getting Juniper Studio's signed website redesign ready for kickoff. Review Maya's replies, resolve the extra Shop page, then approve the brief and publish a simulated handoff.” The visitor is not assigned a fictional name. The signed five-page agreement is summarised beside Project details; contact/owner/service metadata is available there on demand. No visitor account is required.

Dexter accepted the prepared-project entry, project manager visitor role with sample replies, correction-only scope handling and bounded repeat/one-failure coverage on 19 October 2026. Version one has no project/client creation or editing flow, separate client portal, quote or contract-variation workflow. The two projects are prepared fixtures, not an invitation to add project management features.

| Moment | Visitor action | Visible result |
| --- | --- | --- |
| Understand | Read the short role/context introduction. | Project identity, signed scope, owner, status and next action are apparent. |
| Start | Select Start onboarding. | The linked record, client request, review task and fictional welcome message are prepared; the project waits for client answers. |
| Supply reply | Use the labelled Demo tools control to load Maya's sample reply. | Three items await review. Supplying a reply does not approve it. |
| Review | Approve goals/audience and sample asset references, then inspect Pages. | Two items are approved. Shop is flagged against the agreement; kickoff remains blocked. |
| Correct | Enter a comment and select Request changes. Load the corrected reply through Demo tools. | A correction message appears in Outbox. A new Pages revision awaits review; its earlier revision/decision remains visible. Unchanged Goals/Assets approvals remain. |
| Prepare kickoff | Approve the current Pages revision, then select Mark ready for kickoff. | All current required answers are approved. The review task closes, the approved brief is saved and replies/reviews are frozen for this project. |
| Inspect handoff | Review the brief and publish the simulated handoff. | A labelled sandbox register result, matching project reference and confirmation appear separately from readiness. |

The current implemented slice reaches **Inspect handoff** and includes the optional exercises below. Repeat controls and register metadata use collapsed details so the main journey remains available. The supplied correction remains Pages-only; Goals and Assets retain their existing replies/approvals.

Aim for a useful result within two actions from entry and roughly four minutes for the main journey. Validate the under-five-minute criterion with a first-time human reviewer; the mockup does not establish timing.

The public hosting design must not impose a minute-long wake-up wait. Cold-entry and post-inactivity performance must be measured before publication; showing an immediate shell while the usable workflow remains blocked for a minute does not satisfy that requirement.

The employer remains in the project manager role throughout. Sample client replies replace waiting for a real person; they are explicitly demo actions, not a second user-account system or automatic approval.

Shop must appear in the submitted reply so the project manager can review and request its correction. The API accepts that reply into review but rejects its approval against the signed scope. Approving all answers makes Mark ready available; it does not mark the project ready automatically. Repeating Mark ready returns the same saved brief.

## Main project screen

Use the existing restrained workspace. Open on the project rather than an empty list or analytics dashboard.

1. **Project header:** Juniper Studio, OF-1042 and project name. Project details is collapsed by default and contains owner/contact/service plus signed pages.
2. **Stage indicator:** Setup, Client answers, Review and Kickoff. Handoff has its own status; it must not masquerade as readiness.
3. **Blocker or next-action message:** explain what is stopping progress and what the project manager can do about it.
4. **Project tabs:** Onboarding, Kickoff brief, Activity and Outbox.
5. **Central review area:** required-item checklist, selected answer, revision/review status, validation, correction comment and relevant action.
6. **Relevant context:** show the exact signed pages beside the submitted Pages answer. Keep Activity/Outbox and approval/revision metadata in existing tabs/details, with no duplicate activity summary.
7. **Contextual demo actions:** label sample replies at the point they are needed; keep repeat controls collapsed. OF-1043 provides optional recovery. Reset stays separate and names both projects.

The principal action follows the stage: Start onboarding, Load sample reply, approve/request changes beside the selected answer, load corrected Pages, Mark ready and Publish simulated handoff. Mark ready is shown only when eligible. After approving an answer, Review next moves to remaining work. Supplying a reply and changing the selected answer move keyboard focus to that answer; saving readiness moves focus to the brief. The brief puts handoff controls before the longer snapshot. Confirmation feedback reflects committed results.

At narrow widths, stack the content and keep the relevant signed-scope summary near the answer being reviewed. Preserve access to actions and context by keyboard. Use text for status as well as colour. Resolve detailed spacing, component choices and final navigation during the screen's implementation slice, within these boundaries.

## Required interface states

- Initial loading, first use, empty Activity/Outbox and successful setup.
- Waiting for a sample client reply; answers submitted but unapproved.
- Invalid Pages item; missing correction comment; changes requested.
- Corrected reply awaiting approval; all approved but not yet marked ready.
- Ready for kickoff; handoff not published, pending, failed or confirmed.
- A request failure with a safe next action, and an expired session with an explicit restart path.

Pending handoff feedback describes a request awaiting confirmation, not a successful stored publication. If a response is lost, reload the saved state before offering another action; show confirmation only when the register entry is known to exist.

## Employer UI pass: implemented

Dexter's 19 October 2026 review found competing blocks of text and small type. The authorised pass implements the five changes below, using native details and the existing React/CSS. No screens, alternate workflows, component framework or design system were added.

1. **First view:** one short instruction explaining the project manager's task; project name/reference, current stage and one next action. Replace the long introduction and repeated explanations with concise copy.
2. **Context on demand:** collapse owner/contact/service details and repeated agreement context into Project details. Keep the relevant five-page scope beside Pages when reviewing it. Keep the current blocker visible.
3. **Actions by stage:** emphasise the action that advances the current step. Avoid a permanently competing disabled Mark ready button. Show sample replies as labelled demo actions at the moment they are needed. Keep optional recovery/repeat exercises out of the main path.
4. **Readable type and spacing:** use 16px body/action text, at least 14px supporting text and approximately 1.5 line height. Reserve compact labels for short tags. Review both 1280px desktop and 390px phone layouts for comfortable reading and no horizontal overflow.
5. **Evidence without clutter:** keep Activity, Outbox, earlier revisions, approval records and register IDs accessible through existing tabs/details. Retain a concise simulation label and the fixed 24-hour/reset consequence. Do not hide errors or require employers to read a technical explanation to progress.

Desktop/phone browser checks establish the local interaction and typography requirements, and visual captures were inspected. Stop for review. One first-time human walkthrough remains required: identify role/current task, reach a useful result within two actions and finish the main flow in under five minutes without coaching. Record actual time and help required in HARNESS.md. Automated clicks do not establish human understanding or timing.

The final agent-guided review also completed the journey using Review next: Goals -> Pages correction/new approval -> Brand assets -> Ready -> simulated Publish. The checklist supports the alternative review order in the table above. Scope, readiness and handoff remain distinct and understandable. The only optional copy nit is that Waiting for correction could explicitly say Load corrected Pages before Review; its labelled button already provides that action. No further redesign is required by this review.

## Optional visitor exercises

**Repeat:** repeat Start or Publish and inspect the same output identities and counts. Show concise repeat feedback beside the existing result and Activity; replaying a saved action does not create another business event.

**Recover:** use OF-1043 independently of the main path. Welcome attempt one fails while the request and review task remain intact. Show the attempt count and the retained work. Restore the simulated service, then explicitly retry the failed welcome step; the guided example succeeds on attempt two.

Allow three attempts total per project, including the first. Restore changes simulated availability, not the counter; repeated Start does not retry a failure. There are no automatic/timed retries. After three failures show that the budget is exhausted, retain existing work and offer sandbox Reset for a fresh run. Reset affects both sample projects, so make that consequence clear in its label/help text. No reminder scheduler or additional failure variants enter version one.

Both behaviours are required to verify even though visitors may skip the exercises. Version one ends with an application-owned, labelled simulated register result. Real Make/Sheets execution is a later milestone demonstrated separately by Dexter; public visitors cannot launch it.

## Visitor sandbox lifecycle

Explain that the private fictional sandbox lasts 24 hours from creation. Refresh preserves active work; browsing and actions never extend the deadline. Expiry shows a clear Start fresh action and blocks reads/writes to the expired work. Starting fresh does not automatically repeat the action that expired.

Reset replaces the current visitor's two sample projects and clears their old working state; other visitors are unaffected. Ignore late responses from before a reset. Do not promise physical database deletion at the exact 24-hour instant: access stops then, while expired-record cleanup may run later.

## Relationship to the planning mockup

The conversation's screen concept previews sample states and local interactions. It is a design reference, not application source, a functioning API/database or proof of acceptance. Do not copy its canned transitions into the application as substitutes for server-side business rules. The written scope and acceptance criteria govern any later implementation.
