import { useEffect, useRef, useState } from 'react';
import { TriangleAlert } from 'lucide-react';
import type { Project } from './api';
import { ReviewWorkspace } from './onboarding/ReviewWorkspace';
import { useOnboardingWorkflow } from './onboarding/useOnboardingWorkflow';
import { WelcomeRecovery } from './onboarding/WelcomeRecovery';
import { KickoffBrief } from './onboarding/KickoffBrief';
import { ActivityLog, SimulatedOutbox } from './onboarding/WorkflowHistory';

const sections = [
  { key: 'onboarding', label: 'Onboarding' },
  { key: 'brief', label: 'Kickoff brief' },
  { key: 'activity', label: 'Activity' },
  { key: 'outbox', label: 'Outbox' },
] as const;
type Section = (typeof sections)[number]['key'];
const nextAction: Record<string, string> = {
  'Not started': 'Prepare Maya’s request for goals, pages and brand assets.',
  'Waiting for client': 'Load Maya’s sample reply to start your review.',
  'Needs review': 'Review each answer against the signed scope.',
  'Waiting for correction': 'Review Maya’s corrected Pages. Other approvals are kept.',
  'Review complete': 'All answers approved. Save the kickoff brief.',
  'Ready for kickoff': 'Publish the approved brief to the simulated register.',
  'Setup incomplete': 'Your request is saved. The welcome needs attention.',
};

export function OnboardingPanel({
  project,
  onSessionUnavailable,
}: {
  project: Project;
  onSessionUnavailable: (error: unknown) => void;
}) {
  const [section, setSection] = useState<Section>('onboarding');
  const [selected, setSelected] = useState('goals');
  const [comment, setComment] = useState('');
  const commentField = useRef<HTMLTextAreaElement>(null);
  const answerHeading = useRef<HTMLHeadingElement>(null);
  const briefHeading = useRef<HTMLHeadingElement>(null);
  const moveFocus = useRef(false);
  const tabButtons = useRef<(HTMLButtonElement | null)[]>([]);
  const { data, busy, needsReload, error, notice, setError, loadSaved, perform } =
    useOnboardingWorkflow(
      project.id,
      onSessionUnavailable,
      (action, payload) => {
        if (['reply', 'review', 'ready'].includes(action)) moveFocus.current = true;
        setComment('');
        if (action === 'ready') setSection('brief');
        if (action === 'reply') {
          setSection('onboarding');
          setSelected((payload as { sample: string }).sample === 'corrected' ? 'pages' : 'goals');
        }
      },
      (workflow) => {
        if (workflow.brief) setSection('brief');
      },
    );

  useEffect(() => {
    if (!moveFocus.current) return;
    const heading = section === 'brief' ? briefHeading.current : answerHeading.current;
    if (heading) {
      heading.focus();
      moveFocus.current = false;
    }
  }, [data, selected, section]);

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
              <WelcomeRecovery
                onboarding={data.onboarding}
                blocked={blocked}
                onRestore={() =>
                  void perform(
                    'restore',
                    { version },
                    'Simulated service restored. Attempts used are unchanged.',
                  )
                }
                onTryWelcome={() =>
                  void perform(
                    'welcome',
                    { version, expectedAttempts: attempts.length, commandId: crypto.randomUUID() },
                    '',
                  )
                }
              />
            ) : (
              <ReviewWorkspace
                data={data}
                project={project}
                selected={selected}
                comment={comment}
                blocked={blocked}
                answerHeading={answerHeading}
                commentField={commentField}
                onSelect={(kind) => {
                  moveFocus.current = true;
                  setSelected(kind);
                  setComment('');
                  if (!needsReload) setError('');
                }}
                onNext={(kind) => {
                  moveFocus.current = true;
                  setSelected(kind);
                  setComment('');
                }}
                onCommentChange={setComment}
                onReview={review}
                onCorrectedReply={() => {
                  if (current)
                    void perform(
                      'reply',
                      { version, sample: 'corrected', revisionId: current.id },
                      'Corrected Pages received. Review the new revision.',
                    );
                }}
              />
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
          <KickoffBrief
            projectId={project.id}
            brief={data.brief}
            registerEntry={data.registerEntry}
            blocked={blocked}
            headingRef={briefHeading}
            onPublish={() =>
              void perform('publish', {}, 'Simulated handoff confirmed in the sandbox register.')
            }
            onRepeatPublish={() =>
              void perform('publish', {}, 'Same register entry reused. No duplicate handoff.')
            }
          />
        )}

        {section === 'activity' && <ActivityLog events={data.activity} />}
        {section === 'outbox' && <SimulatedOutbox messages={data.outbox} />}
      </div>
    </div>
  );
}
