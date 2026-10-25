# OnboardFlow demo recording script

Draft for review · 19 October 2026 · Main segment target: about three minutes

This is a recording plan, not a completed video or proof of first-time human timing. Record the working local React/C#/PostgreSQL app. Keep the local/demo context visible; do not imply production business use or a real email/Sheets connection. The separately hosted demonstration and its remaining acceptance gates are documented in HARNESS.md.

## Before the take

- Follow the [README setup](../README.md#run-locally) and open http://127.0.0.1:5080/ at a comfortable desktop size, such as 1280 × 900.
- Use a fresh private browser window. If reusing your sandbox, **Reset both sample projects** clears both of your fictional projects; it does not affect other visitors.
- Entry opens directly on the main OF-1042 walkthrough. Keep unrelated tabs, notifications, terminals and credentials outside the recording.
- Rehearse once with this script, then start a fresh sandbox for the take. An edited or rehearsed recording does not satisfy the separate uncoached human walkthrough check.

## Main take

Timing is a pacing guide, not an established completion time. Pause briefly on results so an employer can read them.

| Approximate time | Show and do | Suggested narration |
| --- | --- | --- |
| 0:00–0:15 | Show the opening task and OF-1042. Briefly open Project details to show the five signed pages, then close it. | “OnboardFlow is a personal project built with React, TypeScript, C# and PostgreSQL. You're the project manager, preparing a fictional website project for kickoff.” |
| 0:15–0:35 | Select **Start onboarding**, then **Load Maya's sample reply**. | “The app saves the request and prepares a simulated welcome. This demo control supplies Maya's reply; it doesn't approve her answers.” |
| 0:35–1:10 | **Approve Goals & audience**, then **Review Pages**. Point to Shop and the signed scope. Enter “Please remove Shop and confirm the five signed pages.” Select **Request changes**. | “Shop is outside the agreement. Pages can't be approved until corrected. I record the reason so the request and review history stay together.” |
| 1:10–1:40 | **Load Maya's corrected Pages**, inspect the new revision and **Approve Pages**. Select **Review Brand assets** and **Approve Brand assets**. | “The correction creates a new revision requiring fresh approval. The earlier answer stays in history, and the approved goals are retained.” |
| 1:40–2:10 | Select **Mark ready for kickoff**. Show the saved brief and its five approved pages. | “All current answers are approved. Mark ready saves the approved brief and closes the review task. Readiness is separate from handoff confirmation.” |
| 2:10–2:40 | **Publish simulated handoff**, open **Inspect register entry**, then **Repeat Publish**. Pause on the same register reference. | “Publishing saves a simulated register entry in PostgreSQL. Repeating it reuses the same result. No Make or Google Sheets connection is used.” |
| 2:40–3:00 | Briefly show Activity and Outbox, then return to the brief. | “Activity and Outbox make the saved results inspectable. Automated checks cover the workflow, repeat safety and recovery. The demo runs on Deplexo with PostgreSQL on Neon; long-idle behaviour and first-time human timing remain unmeasured.” |

If the take runs long, shorten narration or omit opening Project details. Keep the correction, new approval, saved brief and separate handoff confirmation intact.

## Optional recovery segment: about 45 seconds

Select **Try failure recovery** below the main walkthrough. The optional exercise opens OF-1043 and explains its separate purpose. Select **Start onboarding**. Pause on “1 of 3 attempts used” and the retained request/review task. Select **Restore simulated service**, show the count is unchanged, then **Try welcome**. The guided example succeeds on attempt two. Open Demo tools / repeat protection to show **2 of 3** attempts used if needed.

Suggested narration: “This exercise shows how the app handles a service failure without losing completed work. The simulated welcome-message service is unavailable, so the welcome deliberately fails while the request and review task stay saved. Restore pretends the service is working again; it doesn't retry anything or reset the budget. Try welcome retries only the failed step and succeeds on attempt two, retaining the original request and task. No real service is contacted or email sent. Three committed failures would exhaust this project's budget until the sandbox is reset.”

Finish with **Back to main walkthrough** to show OF-1042's confirmed brief is unaffected. This segment is optional for the employer and does not add another failure scenario.

## Review before sharing

Check that text is readable, outcomes have time to register, narration matches the saved UI and only fictional data is visible. Describe messages as prepared and the handoff as simulated. Keep the original capture and review the final clip before publication. No video has been recorded or published as part of preparing this script.
