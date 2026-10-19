import type { RefObject } from 'react';
import { CheckCircle2, Clock3, TriangleAlert } from 'lucide-react';
import type { Project } from '../api';
import { formatWorkflowDate as date, type Answer, type Workflow } from './workflow';

function AnswerDetails({ answer }: { answer: Answer }) {
  return (
    <>
      {answer.goal && (
        <>
          <h4>Project goal</h4>
          <p>{answer.goal}</p>
          <h4>Audience</h4>
          <p>{answer.audience}</p>
        </>
      )}
      {answer.pages.length > 0 && (
        <ul className="answer-list">
          {answer.pages.map((page) => (
            <li key={page}>{page}</li>
          ))}
        </ul>
      )}
      {answer.assetReferences.length > 0 && (
        <>
          <p className="muted">Sample references · No uploads</p>
          <ul className="answer-list">
            {answer.assetReferences.map((asset) => (
              <li key={asset}>{asset}</li>
            ))}
          </ul>
        </>
      )}
    </>
  );
}

export function ReviewWorkspace({
  data,
  project,
  selected,
  comment,
  blocked,
  answerHeading,
  commentField,
  onSelect,
  onNext,
  onCommentChange,
  onReview,
  onCorrectedReply,
}: {
  data: Workflow;
  project: Project;
  selected: string;
  comment: string;
  blocked: boolean;
  answerHeading: RefObject<HTMLHeadingElement | null>;
  commentField: RefObject<HTMLTextAreaElement | null>;
  onSelect: (kind: string) => void;
  onNext: (kind: string) => void;
  onCommentChange: (value: string) => void;
  onReview: (decision: 'approved' | 'changes_requested') => void;
  onCorrectedReply: () => void;
}) {
  const item = data.items.find((item) => item.kind === selected);
  const current = item?.revisions.at(-1);
  const ready = data.brief !== null;
  const nextItem = data.items.find(
    (candidate) => candidate.state !== 'Approved' && candidate.kind !== selected,
  );
  return (
    <div className="review-workspace">
      <nav className="required-items" aria-label="Required answers">
        {data.items.map((required) => (
          <button
            key={required.id}
            aria-pressed={selected === required.kind}
            onClick={() => onSelect(required.kind)}
          >
            {required.state === 'Approved' ? (
              <CheckCircle2 size={17} aria-hidden="true" />
            ) : required.validation ? (
              <TriangleAlert size={17} aria-hidden="true" />
            ) : (
              <Clock3 size={17} aria-hidden="true" />
            )}
            <span>
              <strong>{required.label}</strong>
              <span>{required.state}</span>
            </span>
          </button>
        ))}
      </nav>
      <section className="answer-detail" aria-label={item?.label}>
        <h3 ref={answerHeading} tabIndex={-1}>
          {item?.label}
        </h3>
        {!current ? (
          <p className="muted">
            Waiting for Maya’s reply. Use the labelled Demo tools below to supply fictional answers.
          </p>
        ) : (
          <>
            <p className="revision-meta">
              Revision {current.number} · {current.submittedBy} · {date(current.submittedAt)}
            </p>
            <AnswerDetails answer={current.answer} />
            {selected === 'pages' && (
              <div className="agreed-pages">
                <h4>Signed scope · 5 pages</h4>
                <p>{project.signedPages.join(' · ')}</p>
              </div>
            )}
            {item?.validation && (
              <div className="scope-warning">
                <TriangleAlert size={17} aria-hidden="true" />
                <p>{item.validation}</p>
              </div>
            )}
            {current.decision && (
              <p className="review-decision">
                <strong>{item?.state}</strong> by {current.reviewedBy}
                {current.comment && <span>“{current.comment}”</span>}
              </p>
            )}
            {!current.decision && !ready && (
              <div className="review-actions">
                <button
                  className={item?.validation ? 'button secondary' : 'button primary'}
                  disabled={blocked || !!item?.validation}
                  onClick={() => onReview('approved')}
                >
                  Approve {item?.label}
                </button>
                {selected === 'pages' && (
                  <details className="correction-form" open={!!item?.validation} key={current.id}>
                    <summary>Request a correction</summary>
                    <label htmlFor={`${project.id}-comment`}>Correction comment</label>
                    <textarea
                      ref={commentField}
                      id={`${project.id}-comment`}
                      value={comment}
                      maxLength={1000}
                      rows={3}
                      onChange={(event) => onCommentChange(event.target.value)}
                      disabled={blocked}
                    />
                    <button
                      className={item?.validation ? 'button primary' : 'button secondary'}
                      disabled={blocked}
                      onClick={() => onReview('changes_requested')}
                    >
                      Request changes
                    </button>
                  </details>
                )}
              </div>
            )}
            {current.decision === 'changes_requested' && selected === 'pages' && !ready && (
              <div className="reply-tools">
                <span className="simulation-label">Demo · Sample client reply</span>
                <button className="button primary" disabled={blocked} onClick={onCorrectedReply}>
                  Load Maya’s corrected Pages
                </button>
              </div>
            )}
            {current.decision === 'approved' && nextItem && !ready && (
              <button className="button primary next-answer" onClick={() => onNext(nextItem.kind)}>
                Review {nextItem.label}
              </button>
            )}
            {ready && <p className="ready-note">Saved for kickoff · Read-only</p>}
            {item && item.revisions.length > 1 && (
              <details className="revision-history">
                <summary>Earlier revisions ({item.revisions.length - 1})</summary>
                {item.revisions.slice(0, -1).map((revision) => (
                  <article key={revision.id}>
                    <h4>Revision {revision.number}</h4>
                    <AnswerDetails answer={revision.answer} />
                    <p>
                      Reviewed by {revision.reviewedBy}:{' '}
                      {revision.decision === 'changes_requested' ? 'Changes requested' : 'Approved'}
                    </p>
                    {revision.comment && <blockquote>{revision.comment}</blockquote>}
                  </article>
                ))}
              </details>
            )}
          </>
        )}
      </section>
    </div>
  );
}
