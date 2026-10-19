import type { RefObject } from 'react';
import { CheckCircle2 } from 'lucide-react';
import { formatWorkflowDate as date, type Workflow } from './workflow';

export function KickoffBrief({
  projectId,
  brief,
  registerEntry,
  blocked,
  headingRef,
  onPublish,
  onRepeatPublish,
}: {
  projectId: string;
  brief: Workflow['brief'];
  registerEntry: Workflow['registerEntry'];
  blocked: boolean;
  headingRef: RefObject<HTMLHeadingElement | null>;
  onPublish: () => void;
  onRepeatPublish: () => void;
}) {
  return (
    <section className="kickoff-brief" aria-labelledby={`${projectId}-brief-heading`}>
      <h3 id={`${projectId}-brief-heading`} ref={headingRef} tabIndex={-1}>
        Kickoff brief
      </h3>
      {!brief ? (
        <p className="empty-state">
          Approve all three answers, then mark ready to save your brief.
        </p>
      ) : (
        <>
          <p className="brief-saved">
            <CheckCircle2 size={18} aria-hidden="true" />
            Approved brief · saved {date(brief.savedAt)}
          </p>
          <section className="handoff-state" aria-label="Simulated handoff">
            {!registerEntry ? (
              <>
                <p>
                  <strong>Handoff: not published.</strong>
                </p>
                <p>
                  Save an inspectable register entry in your private demo. No external service is
                  contacted.
                </p>
                <button className="button primary" disabled={blocked} onClick={onPublish}>
                  Publish simulated handoff
                </button>
              </>
            ) : (
              <>
                <h4>Simulated handoff confirmed</h4>
                <p>
                  {registerEntry.brief.projectReference} · Sandbox register ·{' '}
                  {date(registerEntry.confirmedAt)}
                </p>
                <details>
                  <summary>Inspect register entry</summary>
                  <p className="register-reference">{registerEntry.reference}</p>
                  <p>{registerEntry.brief.projectName}</p>
                  <ul>
                    {registerEntry.brief.pages.map((page) => (
                      <li key={page}>{page}</li>
                    ))}
                  </ul>
                  <p>
                    Saved copy of the approved kickoff brief in your private demo register. No
                    external service was contacted.
                  </p>
                  <button className="button secondary" disabled={blocked} onClick={onRepeatPublish}>
                    Repeat Publish
                  </button>
                </details>
              </>
            )}
          </section>
          <dl className="brief-overview">
            <div>
              <dt>Project</dt>
              <dd>
                {brief.projectReference} · {brief.projectName}
              </dd>
            </div>
            <div>
              <dt>Client</dt>
              <dd>
                {brief.clientName} · {brief.clientContact}
              </dd>
            </div>
            <div>
              <dt>Owner</dt>
              <dd>{brief.owner}</dd>
            </div>
          </dl>
          <h4>Project goal</h4>
          <p>{brief.goal}</p>
          <h4>Audience</h4>
          <p>{brief.audience}</p>
          <h4>Approved pages</h4>
          <ul className="answer-list">
            {brief.pages.map((page) => (
              <li key={page}>{page}</li>
            ))}
          </ul>
          <h4>Approved asset references</h4>
          <ul className="answer-list">
            {brief.assetReferences.map((asset) => (
              <li key={asset}>{asset}</li>
            ))}
          </ul>
          <details>
            <summary>Approval record</summary>
            <ul>
              {brief.approvals.map((approval) => (
                <li key={approval.revisionId}>
                  {approval.kind === 'goals'
                    ? 'Goals & audience'
                    : approval.kind === 'assets'
                      ? 'Brand assets'
                      : 'Pages'}{' '}
                  · revision {approval.number} · {approval.reviewedBy} · {date(approval.reviewedAt)}
                </li>
              ))}
            </ul>
          </details>
        </>
      )}
    </section>
  );
}
