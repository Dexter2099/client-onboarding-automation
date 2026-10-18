import { useEffect, useRef, useState } from 'react';
import { CheckCircle2, Clock3, TriangleAlert } from 'lucide-react';
import { api, ApiError, type Project } from './api';

type Answer = {
  goal: string | null;
  audience: string | null;
  pages: string[];
  assetReferences: string[];
};
type Revision = {
  id: string;
  number: number;
  answer: Answer;
  submittedBy: string;
  submittedAt: string;
  decision: string | null;
  comment: string | null;
  reviewedBy: string | null;
  reviewedAt: string | null;
};
type Item = {
  id: string;
  kind: string;
  label: string;
  state: string;
  validation: string | null;
  revisions: Revision[];
};
type Brief = {
  projectReference: string;
  projectName: string;
  clientName: string;
  clientContact: string;
  owner: string;
  service: string;
  goal: string;
  audience: string;
  pages: string[];
  assetReferences: string[];
  approvals: {
    kind: string;
    revisionId: string;
    number: number;
    reviewedBy: string;
    reviewedAt: string;
  }[];
  savedAt: string;
};
type Workflow = {
  projectId: string;
  state: string;
  canMarkReady: boolean;
  onboarding: {
    id: string;
    requestId: string;
    reviewTaskId: string;
    version: number;
    welcomePreparedAt: string | null;
    readyAt: string | null;
    reviewTaskClosedAt: string | null;
    welcomeServiceAvailable: boolean;
    welcomeAttempts: {
      id: string;
      number: number;
      commandId: string;
      prepared: boolean;
      at: string;
    }[];
  } | null;
  items: Item[];
  brief: Brief | null;
  registerEntry: { id: string; reference: string; confirmedAt: string; brief: Brief } | null;
  outbox: { id: string; recipient: string; subject: string; body: string; preparedAt: string }[];
  activity: {
    id: string;
    sequence: number;
    actor: string;
    action: string;
    outcome: string;
    details: string;
    at: string;
  }[];
};
const sections = [
  { key: 'onboarding', label: 'Onboarding' },
  { key: 'brief', label: 'Kickoff brief' },
  { key: 'activity', label: 'Activity' },
  { key: 'outbox', label: 'Outbox' },
] as const;
type Section = (typeof sections)[number]['key'];
const date = (value: string) =>
  new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
const nextAction: Record<string, string> = {
  'Not started': 'Prepare Maya’s request for goals, pages and brand assets.',
  'Waiting for client': 'Load Maya’s sample reply to start your review.',
  'Needs review': 'Review each answer against the signed scope.',
  'Waiting for correction': 'Review Maya’s corrected Pages. Other approvals are kept.',
  'Review complete': 'All answers approved. Save the kickoff brief.',
  'Ready for kickoff': 'Publish the approved brief to the simulated register.',
  'Setup incomplete': 'Your request is saved. The welcome needs attention.',
};

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

export function OnboardingPanel({
  project,
  onSessionUnavailable,
}: {
  project: Project;
  onSessionUnavailable: (error: unknown) => void;
}) {
  const [data, setData] = useState<Workflow | null>(null);
  const [busy, setBusy] = useState(true);
  const [needsReload, setNeedsReload] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [section, setSection] = useState<Section>('onboarding');
  const [selected, setSelected] = useState('goals');
  const [comment, setComment] = useState('');
  const alive = useRef(false);
  const commentField = useRef<HTMLTextAreaElement>(null);
  const answerHeading = useRef<HTMLHeadingElement>(null);
  const briefHeading = useRef<HTMLHeadingElement>(null);
  const moveFocus = useRef(false);
  const tabButtons = useRef<(HTMLButtonElement | null)[]>([]);
  const path = `/projects/${project.id}`;

  function report(error: unknown) {
    if (!alive.current) return;
    if (error instanceof ApiError && error.status === 401) onSessionUnavailable(error);
    else setError(error instanceof Error ? error.message : 'Could not load onboarding.');
  }

  useEffect(() => {
    let cancelled = false;
    alive.current = true;
    api<Workflow>(`${path}/onboarding`)
      .then((result) => {
        if (cancelled) return;
        setData(result);
        if (result.brief) setSection('brief');
      })
      .catch((error) => {
        if (!cancelled) report(error);
      })
      .finally(() => {
        if (!cancelled) setBusy(false);
      });
    return () => {
      cancelled = true;
      alive.current = false;
    };
  }, [project.id]);

  useEffect(() => {
    if (!moveFocus.current) return;
    const heading = section === 'brief' ? briefHeading.current : answerHeading.current;
    if (heading) {
      heading.focus();
      moveFocus.current = false;
    }
  }, [data, selected, section]);

  async function loadSaved() {
    setBusy(true);
    setError('');
    try {
      const result = await api<Workflow>(`${path}/onboarding`);
      if (alive.current) {
        setData(result);
        setNeedsReload(false);
      }
    } catch (error) {
      report(error);
    } finally {
      if (alive.current) setBusy(false);
    }
  }

  async function perform(action: string, payload: unknown, message: string) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await api<Workflow>(`${path}/${action}`, payload);
      if (!alive.current) return;
      if (['reply', 'review', 'ready'].includes(action)) moveFocus.current = true;
      setData(result);
      setNotice(
        (action === 'start' && !data?.onboarding) || action === 'welcome'
          ? result.onboarding?.welcomePreparedAt
            ? 'Welcome prepared in the simulated Outbox.'
            : 'Welcome unavailable. Your request and review task are saved.'
          : message,
      );
      setComment('');
      if (action === 'ready') setSection('brief');
      if (action === 'reply') {
        setSection('onboarding');
        setSelected((payload as { sample: string }).sample === 'corrected' ? 'pages' : 'goals');
      }
    } catch (error) {
      report(error);
      // An uncertain response requires a read before any new command can spend another attempt.
      setNeedsReload(!(error instanceof ApiError) || error.status >= 500);
      if (alive.current && error instanceof ApiError && error.status === 409) {
        try {
          const saved = await api<Workflow>(`${path}/onboarding`);
          if (alive.current) {
            setData(saved);
            setNeedsReload(false);
          }
        } catch (reloadError) {
          report(reloadError);
          setNeedsReload(true);
        }
      }
    } finally {
      if (alive.current) setBusy(false);
    }
  }

  if (!data)
    return (
      <div className="workflow-loading" aria-live="polite">
        {busy ? 'Loading onboarding…' : error}
        {!busy && (
          <button className="button secondary" onClick={() => void loadSaved()}>
            Load saved state
          </button>
        )}
      </div>
    );

  const item = data.items.find((item) => item.kind === selected);
  const current = item?.revisions.at(-1);
  const ready = data.brief !== null;
  const approved = data.items.filter((item) => item.state === 'Approved').length;
  const initialLoaded = data.items.some((item) => item.revisions.length > 0);
  const version = data.onboarding?.version ?? 0;
  const blocked = busy || needsReload;
  const attempts = data.onboarding?.welcomeAttempts ?? [];
  const stage = ready ? 3 : !data.onboarding?.welcomePreparedAt ? 0 : !initialLoaded ? 1 : 2;
  const nextItem = data.items.find(
    (candidate) => candidate.state !== 'Approved' && candidate.kind !== selected,
  );

  function review(decision: string) {
    if (!current) return;
    if (decision === 'changes_requested' && !comment.trim()) {
      setError('Add a correction comment before requesting changes.');
      commentField.current?.focus();
      return;
    }
    void perform(
      'review',
      {
        version,
        revisionId: current.id,
        decision,
        comment: decision === 'changes_requested' ? comment : null,
      },
      decision === 'approved'
        ? `${item!.label} approved.`
        : 'Correction requested. A simulated message is in Outbox.',
    );
  }

  return (
    <div className="workflow">
      <div className="workflow-header">
        <div>
          <p className="eyebrow">{data.state}</p>
          <p id="next-action">
            {data.registerEntry
              ? 'Handoff confirmed. Your approved brief is saved.'
              : nextAction[data.state]}
          </p>
          {initialLoaded && !ready && <p className="muted">{approved} of 3 answers approved</p>}
        </div>
        {!data.onboarding ? (
          <button
            className="button primary"
            disabled={blocked}
            onClick={() =>
              void perform(
                'start',
                {},
                'Onboarding prepared. The fictional welcome message is in Outbox.',
              )
            }
          >
            Start onboarding
          </button>
        ) : !initialLoaded && data.onboarding.welcomePreparedAt ? (
          <div className="next-step-action">
            <span className="simulation-label">Demo · Sample client reply</span>
            <button
              className="button primary"
              disabled={blocked}
              onClick={() =>
                void perform(
                  'reply',
                  { version, sample: 'initial' },
                  'Maya’s sample reply is awaiting review.',
                )
              }
            >
              Load Maya’s sample reply
            </button>
          </div>
        ) : data.canMarkReady ? (
          <button
            className="button primary"
            disabled={blocked}
            aria-describedby="next-action"
            onClick={() => void perform('ready', { version }, 'Approved kickoff brief saved.')}
          >
            Mark ready for kickoff
          </button>
        ) : ready && section !== 'brief' ? (
          <button className="button primary" onClick={() => setSection('brief')}>
            View kickoff brief
          </button>
        ) : !ready && initialLoaded && section !== 'onboarding' ? (
          <button className="button primary" onClick={() => setSection('onboarding')}>
            Continue review
          </button>
        ) : null}
      </div>
      <ol className="stages" aria-label="Onboarding stages">
        {['Setup', 'Client answers', 'Review', 'Kickoff'].map((name, index) => (
          <li
            key={name}
            className={index <= stage ? 'reached' : ''}
            aria-current={index === stage ? 'step' : undefined}
          >
            <span aria-hidden="true">{index < stage ? '✓' : index + 1}</span>
            {name}
          </li>
        ))}
      </ol>
      <div className="workflow-notice" aria-live="polite">
        {busy ? 'Saving or loading project state…' : notice}
      </div>
      {error && (
        <div className="workflow-error" role="alert">
          <TriangleAlert size={18} aria-hidden="true" />
          <div>
            <p>{error}</p>
            <button className="text-button" disabled={busy} onClick={() => void loadSaved()}>
              Load saved state
            </button>
          </div>
        </div>
      )}

      <div className="project-tabs" role="tablist" aria-label="Project sections">
        {sections.map((tab, index) => (
          <button
            key={tab.key}
            role="tab"
            ref={(element) => {
              tabButtons.current[index] = element;
            }}
            id={`${project.id}-${tab.key}-tab`}
            aria-controls={`${project.id}-panel`}
            aria-selected={section === tab.key}
            tabIndex={section === tab.key ? 0 : -1}
            onClick={() => setSection(tab.key)}
            onKeyDown={(event) => {
              const next =
                event.key === 'ArrowRight'
                  ? (index + 1) % sections.length
                  : event.key === 'ArrowLeft'
                    ? (index + sections.length - 1) % sections.length
                    : event.key === 'Home'
                      ? 0
                      : event.key === 'End'
                        ? sections.length - 1
                        : null;
              if (next !== null) {
                event.preventDefault();
                setSection(sections[next].key);
                tabButtons.current[next]?.focus();
              }
            }}
          >
            {tab.label}
            {tab.key === 'outbox' && <span className="tab-count">{data.outbox.length}</span>}
          </button>
        ))}
      </div>
      <div
        className="project-tab-panel"
        id={`${project.id}-panel`}
        role="tabpanel"
        aria-labelledby={`${project.id}-${section}-tab`}
      >
        {section === 'onboarding' && (
          <>
            {!data.onboarding || (data.onboarding.welcomePreparedAt && !initialLoaded) ? (
              <div className="empty-state">
                <h3>Three essentials for kickoff</h3>
                <ul className="essentials">
                  <li>Goals &amp; audience</li>
                  <li>Agreed pages</li>
                  <li>Brand assets</li>
                </ul>
              </div>
            ) : !data.onboarding.welcomePreparedAt ? (
              <section className="welcome-recovery" aria-label="Welcome recovery">
                <h3>
                  {attempts.length >= 3 ? 'Welcome attempts exhausted' : 'Welcome not prepared'}
                </h3>
                <p>{attempts.length} of 3 attempts used · Request and review task saved</p>
                <p>
                  {attempts.length >= 3
                    ? 'Your work is retained. Reset both sample projects below for a fresh sandbox.'
                    : data.onboarding.welcomeServiceAvailable
                      ? 'The simulated service is available. Try welcome to continue.'
                      : 'Restore the simulated service, then try the welcome again.'}
                </p>
                <div className="recovery-actions">
                  <button
                    className={
                      data.onboarding.welcomeServiceAvailable
                        ? 'button secondary'
                        : 'button primary'
                    }
                    disabled={blocked || data.onboarding.welcomeServiceAvailable}
                    onClick={() =>
                      void perform(
                        'restore',
                        { version },
                        'Simulated service restored. Attempts used are unchanged.',
                      )
                    }
                  >
                    {data.onboarding.welcomeServiceAvailable
                      ? 'Simulated service available'
                      : 'Restore simulated service'}
                  </button>
                  <button
                    className={
                      data.onboarding.welcomeServiceAvailable
                        ? 'button primary'
                        : 'button secondary'
                    }
                    disabled={blocked || attempts.length >= 3}
                    onClick={() =>
                      void perform(
                        'welcome',
                        {
                          version,
                          expectedAttempts: attempts.length,
                          commandId: crypto.randomUUID(),
                        },
                        '',
                      )
                    }
                  >
                    Try welcome
                  </button>
                </div>
                <details>
                  <summary>Welcome attempts</summary>
                  <ol>
                    {attempts.map((attempt) => (
                      <li key={attempt.id}>
                        Attempt {attempt.number}: {attempt.prepared ? 'Prepared' : 'Failed'} ·{' '}
                        {date(attempt.at)}
                      </li>
                    ))}
                  </ol>
                </details>
                <span className="simulation-label">
                  Demo control · No email is sent. Restore does not reset attempts.
                </span>
              </section>
            ) : (
              <div className="review-workspace">
                <nav className="required-items" aria-label="Required answers">
                  {data.items.map((required) => (
                    <button
                      key={required.id}
                      aria-pressed={selected === required.kind}
                      onClick={() => {
                        moveFocus.current = true;
                        setSelected(required.kind);
                        setComment('');
                        if (!needsReload) setError('');
                      }}
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
                      Waiting for Maya’s reply. Use the labelled Demo tools below to supply
                      fictional answers.
                    </p>
                  ) : (
                    <>
                      <p className="revision-meta">
                        Revision {current.number} · {current.submittedBy} ·{' '}
                        {date(current.submittedAt)}
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
                            onClick={() => review('approved')}
                          >
                            Approve {item?.label}
                          </button>
                          {selected === 'pages' && (
                            <details
                              className="correction-form"
                              open={!!item?.validation}
                              key={current.id}
                            >
                              <summary>Request a correction</summary>
                              <label htmlFor={`${project.id}-comment`}>Correction comment</label>
                              <textarea
                                ref={commentField}
                                id={`${project.id}-comment`}
                                value={comment}
                                maxLength={1000}
                                rows={3}
                                onChange={(event) => setComment(event.target.value)}
                                disabled={blocked}
                              />
                              <button
                                className={item?.validation ? 'button primary' : 'button secondary'}
                                disabled={blocked}
                                onClick={() => review('changes_requested')}
                              >
                                Request changes
                              </button>
                            </details>
                          )}
                        </div>
                      )}
                      {current.decision === 'changes_requested' &&
                        selected === 'pages' &&
                        !ready && (
                          <div className="reply-tools">
                            <span className="simulation-label">Demo · Sample client reply</span>
                            <button
                              className="button primary"
                              disabled={blocked}
                              onClick={() =>
                                void perform(
                                  'reply',
                                  { version, sample: 'corrected', revisionId: current.id },
                                  'Corrected Pages received. Review the new revision.',
                                )
                              }
                            >
                              Load Maya’s corrected Pages
                            </button>
                          </div>
                        )}
                      {current.decision === 'approved' && nextItem && !ready && (
                        <button
                          className="button primary next-answer"
                          onClick={() => {
                            moveFocus.current = true;
                            setSelected(nextItem.kind);
                            setComment('');
                          }}
                        >
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
                                {revision.decision === 'changes_requested'
                                  ? 'Changes requested'
                                  : 'Approved'}
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
            )}
            {data.onboarding && (
              <details className="repeat-tools">
                <summary>Demo tools · repeat protection</summary>
                <p>Repeat Start to check that the saved request, task and welcome are reused.</p>
                <button
                  className="button secondary"
                  disabled={blocked}
                  onClick={() =>
                    void perform(
                      'start',
                      {},
                      'Saved onboarding reused. No new setup or welcome attempt.',
                    )
                  }
                >
                  Repeat Start
                </button>
                <dl>
                  <div>
                    <dt>Request</dt>
                    <dd>{data.onboarding.requestId}</dd>
                  </div>
                  <div>
                    <dt>Review task</dt>
                    <dd>{data.onboarding.reviewTaskId}</dd>
                  </div>
                  <div>
                    <dt>Welcome attempts used</dt>
                    <dd>{attempts.length} of 3</dd>
                  </div>
                </dl>
              </details>
            )}
          </>
        )}

        {section === 'brief' && (
          <section className="kickoff-brief" aria-labelledby={`${project.id}-brief-heading`}>
            <h3 id={`${project.id}-brief-heading`} ref={briefHeading} tabIndex={-1}>
              Kickoff brief
            </h3>
            {!data.brief ? (
              <p className="empty-state">
                Approve all three answers, then mark ready to save your brief.
              </p>
            ) : (
              <>
                <p className="brief-saved">
                  <CheckCircle2 size={18} aria-hidden="true" />
                  Approved brief · saved {date(data.brief.savedAt)}
                </p>
                <section className="handoff-state" aria-label="Simulated handoff">
                  {!data.registerEntry ? (
                    <>
                      <p>
                        <strong>Handoff: not published.</strong>
                      </p>
                      <p>Save the approved brief to this sandbox. No external connection.</p>
                      <button
                        className="button primary"
                        disabled={blocked}
                        onClick={() =>
                          void perform(
                            'publish',
                            {},
                            'Simulated handoff confirmed in the sandbox register.',
                          )
                        }
                      >
                        Publish simulated handoff
                      </button>
                    </>
                  ) : (
                    <>
                      <h4>Simulated handoff confirmed</h4>
                      <p>
                        {data.registerEntry.brief.projectReference} · Sandbox register ·{' '}
                        {date(data.registerEntry.confirmedAt)}
                      </p>
                      <details>
                        <summary>Inspect register entry</summary>
                        <p className="register-reference">{data.registerEntry.reference}</p>
                        <p>{data.registerEntry.brief.projectName}</p>
                        <ul>
                          {data.registerEntry.brief.pages.map((page) => (
                            <li key={page}>{page}</li>
                          ))}
                        </ul>
                        <p>
                          Snapshot of the approved kickoff brief. No external connection was used.
                        </p>
                        <button
                          className="button secondary"
                          disabled={blocked}
                          onClick={() =>
                            void perform(
                              'publish',
                              {},
                              'Same register entry reused. No duplicate handoff.',
                            )
                          }
                        >
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
                      {data.brief.projectReference} · {data.brief.projectName}
                    </dd>
                  </div>
                  <div>
                    <dt>Client</dt>
                    <dd>
                      {data.brief.clientName} · {data.brief.clientContact}
                    </dd>
                  </div>
                  <div>
                    <dt>Owner</dt>
                    <dd>{data.brief.owner}</dd>
                  </div>
                </dl>
                <h4>Project goal</h4>
                <p>{data.brief.goal}</p>
                <h4>Audience</h4>
                <p>{data.brief.audience}</p>
                <h4>Approved pages</h4>
                <ul className="answer-list">
                  {data.brief.pages.map((page) => (
                    <li key={page}>{page}</li>
                  ))}
                </ul>
                <h4>Approved asset references</h4>
                <ul className="answer-list">
                  {data.brief.assetReferences.map((asset) => (
                    <li key={asset}>{asset}</li>
                  ))}
                </ul>
                <details>
                  <summary>Approval record</summary>
                  <ul>
                    {data.brief.approvals.map((approval) => (
                      <li key={approval.revisionId}>
                        {approval.kind === 'goals'
                          ? 'Goals & audience'
                          : approval.kind === 'assets'
                            ? 'Brand assets'
                            : 'Pages'}{' '}
                        · revision {approval.number} · {approval.reviewedBy} ·{' '}
                        {date(approval.reviewedAt)}
                      </li>
                    ))}
                  </ul>
                </details>
              </>
            )}
          </section>
        )}

        {section === 'activity' && (
          <section aria-label="Project activity">
            <h3>Activity</h3>
            {data.activity.length === 0 ? (
              <p className="empty-state">No project activity yet.</p>
            ) : (
              <ol className="activity-list">
                {data.activity.map((event) => (
                  <li key={event.id}>
                    <h4>{event.action}</h4>
                    <p className="revision-meta">
                      {event.actor} · {date(event.at)} · {event.outcome}
                    </p>
                    <p>{event.details}</p>
                  </li>
                ))}
              </ol>
            )}
          </section>
        )}
        {section === 'outbox' && (
          <section aria-label="Simulated outbox">
            <h3>Outbox</h3>
            <p className="muted">Simulated messages, prepared in this sandbox. No email is sent.</p>
            {data.outbox.length === 0 ? (
              <p className="empty-state">No messages prepared yet.</p>
            ) : (
              data.outbox.map((message) => (
                <article className="outbox-message" key={message.id}>
                  <span className="simulation-label">Simulated · prepared only</span>
                  <h4>{message.subject}</h4>
                  <p className="revision-meta">
                    To {message.recipient} · {date(message.preparedAt)}
                  </p>
                  <p>{message.body}</p>
                </article>
              ))
            )}
          </section>
        )}
      </div>
    </div>
  );
}
