import { Instructions } from './Instructions';
import { RequestNavigation } from './RequestNavigation';
import { RequestFieldInput } from './RequestFieldInput';
import { SourceDocument } from './SourceDocument';
import { useEffect, useRef, useState } from 'react';
import {
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  ChevronRight,
  FileText,
  Home,
  Menu,
  MessageCircle,
  Sparkles,
} from 'lucide-react';
import { documents, fieldList, fields, taskFields, taxReply } from './demo-data';
import type { Evidence, Page, RequestedField } from './demo-data';
import { initialState, isAnswered, isSubmitted } from './state';
import { IntakeApi, IntakeRequestCancelled } from './api';
import type { RequestSnapshot } from './api';
import type { Answer } from './state';

const pathToPage = (): Page =>
  location.pathname.includes('onboarding-task-list')
    ? 'tasks'
    : location.pathname.includes('general-information')
      ? 'general'
      : 'instructions';
const route = (page: Page) =>
  '/l/' +
  { instructions: 'instructions', general: 'general-information', tasks: 'onboarding-task-list' }[
    page
  ];

export function App() {
  const [state, setState] = useState(() => ({
    ...initialState(),
    page: pathToPage(),
    selected: pathToPage() === 'general' ? 'legal' : 'intro',
  }));
  const api = useRef(new IntakeApi());
  const editSequence = useRef(0);
  const activeRequest = useRef(false);
  const [apiError, setApiError] = useState('');
  const [source, setSource] = useState<Evidence | null>(null);
  const [sourceField, setSourceField] = useState<string | null>(null);
  const [questionOpen, setQuestionOpen] = useState(false);
  const [question, setQuestion] = useState('');
  const [message, setMessage] = useState('');
  const [mobileNav, setMobileNav] = useState(false);
  const [fillingLabel, setFillingLabel] = useState('');
  const [traceOpen, setTraceOpen] = useState(false);
  const [notice, setNotice] = useState('');
  const [checks, setChecks] = useState<string[]>([]);
  const fillGeneration = useRef(0);
  const mainHeading = useRef<HTMLHeadingElement>(null);
  const resetDialog = useRef<HTMLDialogElement>(null);
  const [resetting, setResetting] = useState(false);
  function reportApiError(error: unknown) {
    if (!(error instanceof IntakeRequestCancelled)) setApiError((error as Error).message);
  }
  function inspectSource(value: Evidence, fieldName?: string) {
    setSourceField(fieldName || null);
    setSource(value);
  }
  const selected = fields.find((field) => field.id === state.selected);
  const currentFields = fieldList(state.page);
  const totalSubmitted = fields.filter((field) => isSubmitted(state.answers[field.id])).length;
  const busy = state.ai === 'filling';
  const complete = totalSubmitted === fields.length;

  useEffect(() => {
    let current = true;
    api.current
      .initialize()
      .then((snapshot) => {
        if (current) applySnapshot(snapshot);
      })
      .catch((error) => {
        if (current) reportApiError(error);
      });
    return () => {
      current = false;
    };
  }, []);
  useEffect(() => {
    document.querySelector('.field-link.selected')?.scrollIntoView({ block: 'nearest' });
  }, [state.selected]);
  useEffect(
    () => () => {
      fillGeneration.current++;
    },
    [],
  );
  useEffect(() => {
    const onPop = () => {
      setState((previous) => ({
        ...previous,
        page: pathToPage(),
        selected: pathToPage() === 'general' ? 'legal' : 'intro',
      }));
    };
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, []);
  useEffect(() => {
    setQuestionOpen(false);
    setMessage('');
    const field = fields.find((item) => item.id === state.selected);
    setChecks(field?.confirmation && state.answers[field.id]?.value ? field.confirmation : []);
  }, [state.selected]);

  function navigate(page: Page, id = page === 'general' ? 'legal' : 'intro', focus = false) {
    history.pushState(null, '', route(page));
    setState((previous) => ({ ...previous, page, selected: id }));
    setNotice('');
    setMobileNav(false);
    if (focus) requestAnimationFrame(() => mainHeading.current?.focus());
  }
  function nextField() {
    const index = currentFields.findIndex((field) => field.id === state.selected);
    navigate(
      state.page,
      index < currentFields.length - 1 ? currentFields[index + 1].id : 'summary',
      true,
    );
  }
  function applySnapshot(snapshot: RequestSnapshot) {
    setState((previous) =>
      snapshot.version < previous.version ||
      (snapshot.version === previous.version && snapshot.ai === previous.ai)
        ? previous
        : { ...previous, ...snapshot },
    );
    if (snapshot.run?.error) setApiError(snapshot.run.error);
    const trace = snapshot.run?.trace.at(-1);
    if (trace) setFillingLabel(trace.detail);
  }
  async function reloadSaved() {
    try {
      setApiError('');
      applySnapshot(await api.current.reload());
    } catch (error) {
      reportApiError(error);
    }
  }
  function writeAnswer(id: string, value: string) {
    const sequence = ++editSequence.current;
    setNotice('');
    setState((previous) => ({
      ...previous,
      answers: { ...previous.answers, [id]: { value, author: 'you', status: 'draft' } },
    }));
    void api.current
      .command('edit', { field: id, value })
      .then((snapshot) => {
        if (sequence === editSequence.current) applySnapshot(snapshot);
      })
      .catch(reportApiError);
  }
  function validAnswer(field: RequestedField, answer?: Answer): boolean {
    if (!isAnswered(answer)) return false;
    if (field.kind === 'email') return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(answer!.value);
    if (field.kind === 'url') return /^https?:\/\/[^\s]+\.[^\s]+$/.test(answer!.value);
    if (field.kind === 'tel') return answer!.value.replace(/\D/g, '').length >= 10;
    return true;
  }
  async function submit(field: RequestedField) {
    if (!validAnswer(field, state.answers[field.id])) {
      setNotice('Add a valid answer before submitting.');
      return;
    }
    try {
      applySnapshot(await api.current.command('submit', { field: field.id }));
      nextField();
    } catch (error) {
      reportApiError(error);
    }
  }
  async function submitPage() {
    const missing = currentFields.find((field) => !validAnswer(field, state.answers[field.id]));
    if (missing) {
      navigate(state.page, missing.id, true);
      setNotice('This item still needs an answer.');
      return;
    }
    try {
      applySnapshot(await api.current.command('submitPage', { page: state.page }));
      setNotice('Page submitted for review.');
    } catch (error) {
      reportApiError(error);
    }
  }
  async function runAgent(resume: boolean) {
    if (activeRequest.current) return;
    activeRequest.current = true;
    const generation = ++fillGeneration.current;
    setApiError('');
    setState((previous) => ({ ...previous, ai: 'filling' }));
    setFillingLabel('Reading the required fields and client documents…');
    let polling = false;
    let finished = false;
    const observedAnswers = new Set(Object.keys(state.answers));
    const timer = setInterval(async () => {
      if (polling) return;
      polling = true;
      try {
        const snapshot = await api.current.read();
        if (generation === fillGeneration.current && !finished) {
          applySnapshot(snapshot);
          const ids = Object.keys(snapshot.answers).filter((id) => !observedAnswers.has(id));
          ids.forEach((id) => observedAnswers.add(id));
          const latest = fields.find((field) => field.id === ids.at(-1));
          if (latest && snapshot.ai === 'filling') navigate(latest.page, latest.id);
        }
      } catch {
        /* The explicit final result/read reports errors; never replay the start. */
      } finally {
        polling = false;
      }
    }, 900);
    try {
      const snapshot = await api.current.agent(resume);
      finished = true;
      if (generation !== fillGeneration.current) return;
      applySnapshot(snapshot);
      const pending = fields.find((field) => field.id === snapshot.run?.question?.fieldId);
      if (pending) navigate(pending.page, pending.id, true);
      else if (snapshot.ai === 'complete') navigate('general', 'summary', true);
    } catch (error) {
      finished = true;
      if (generation !== fillGeneration.current || error instanceof IntakeRequestCancelled) return;
      try {
        applySnapshot(await api.current.reload());
      } catch {
        /* Keep the original failure visible. */
      }
      reportApiError(error);
    } finally {
      finished = true;
      clearInterval(timer);
      if (generation === fillGeneration.current) activeRequest.current = false;
    }
  }
  const fillFromDocument = () => runAgent(false);
  const resume = () => runAgent(true);
  async function supplyReply() {
    try {
      applySnapshot(await api.current.command('reply'));
    } catch (error) {
      reportApiError(error);
    }
  }
  async function saveQuestion() {
    if (!selected) return;
    try {
      applySnapshot(
        await api.current.command('question', { field: selected.id, value: question.trim() }),
      );
      setMessage('Question saved in this demo. No message was sent.');
      setQuestionOpen(false);
      setQuestion('');
    } catch (error) {
      reportApiError(error);
    }
  }
  async function reset() {
    if (resetting) return;
    setResetting(true);
    fillGeneration.current++;
    activeRequest.current = false;
    try {
      const snapshot = await api.current.reset();
      setState({ ...initialState(), ...snapshot });
      navigate('instructions', 'intro', true);
      setSource(null);
      setSourceField(null);
      setQuestionOpen(false);
      setQuestion('');
      setMessage('');
      setFillingLabel('');
      setTraceOpen(false);
      setChecks([]);
      setNotice('');
      setApiError('');
    } catch (error) {
      reportApiError(error);
    } finally {
      setResetting(false);
      resetDialog.current?.close();
    }
  }
  const requestReset = () => resetDialog.current?.showModal();
  const statusFor = (field: RequestedField) =>
    isSubmitted(state.answers[field.id])
      ? 'submitted'
      : isAnswered(state.answers[field.id])
        ? 'draft'
        : field.id === state.run?.question?.fieldId && state.ai === 'waiting'
          ? 'missing'
          : 'empty';
  const submittedCount = (page: Page) =>
    fieldList(page).filter((field) => isSubmitted(state.answers[field.id])).length;

  return (
    <div className="request-app">
      <div className="mobile-bar">
        <button
          aria-label="Open request navigation"
          type="button"
          onClick={() => setMobileNav(!mobileNav)}
        >
          <Menu size={21} />
        </button>
        <span>OnboardFlow</span>
        <button type="button" className="reset-demo" onClick={requestReset}>
          Reset demo
        </button>
        <span className="status-badge">ACTIVE</span>
      </div>
      <RequestNavigation
        state={state}
        mobileNav={mobileNav}
        busy={busy}
        complete={complete}
        navigate={navigate}
        statusFor={statusFor}
        submittedCount={submittedCount}
        onReset={requestReset}
      />
      <main className="workspace">
        {apiError && (
          <div className="api-error" role="alert">
            {apiError}{' '}
            <button type="button" onClick={() => void reloadSaved()}>
              Load saved work
            </button>
            {apiError.includes('expired') && (
              <button
                type="button"
                onClick={async () => {
                  fillGeneration.current++;
                  try {
                    const snapshot = await api.current.fresh();
                    setState(initialState());
                    applySnapshot(snapshot);
                    setApiError('');
                  } catch (error) {
                    reportApiError(error);
                  }
                }}
              >
                Start fresh workspace
              </button>
            )}
          </div>
        )}
        {state.page !== 'instructions' && (
          <div className="top-nav">
            <button
              type="button"
              onClick={() => navigate(state.page === 'general' ? 'instructions' : 'general')}
              disabled={busy}
            >
              <ArrowLeft size={14} />
              {state.page === 'general' ? 'Instructions' : 'General Information'}
            </button>
            {state.page === 'general' && (
              <button type="button" onClick={() => navigate('tasks')} disabled={busy}>
                Onboarding Task List
                <ArrowRight size={14} />
              </button>
            )}
          </div>
        )}
        <div className={'canvas ' + (state.page === 'instructions' ? 'instruction-canvas' : '')}>
          {state.page === 'instructions' ? (
            <Instructions
              mainHeading={mainHeading}
              onStart={() => navigate('general', 'legal', true)}
            />
          ) : (
            <>
              <section className="ai-bar" aria-label="AI document filling">
                <div className="ai-file">
                  <FileText size={22} />
                  <div>
                    <button
                      type="button"
                      className="document-name"
                      onClick={() =>
                        inspectSource({
                          document: 'profile',
                          section: 'company',
                          quote: documents.profile.sections[0].text,
                        })
                      }
                    >
                      Client information pack
                    </button>
                    <span className="small muted">
                      2 simulated documents · provided by Maya Chen
                    </span>
                  </div>
                </div>
                {state.ai === 'idle' || state.ai === 'failed' || state.ai === 'interrupted' ? (
                  <button type="button" className="primary" onClick={fillFromDocument}>
                    <Sparkles size={15} />
                    FILL WITH AI
                  </button>
                ) : (
                  <div className="ai-state" role="status">
                    {busy ? (
                      <>
                        <span className="working-dot" />
                        {fillingLabel || 'Reading client documents…'}
                      </>
                    ) : state.ai === 'waiting' ? (
                      'AI paused · 1 missing detail'
                    ) : (
                      'AI drafts ready · review before submitting'
                    )}
                  </div>
                )}
              </section>
              {state.ai !== 'idle' && !busy && (
                <details
                  className="ai-activity"
                  open={traceOpen}
                  onToggle={(event) => setTraceOpen(event.currentTarget.open)}
                >
                  <summary>
                    AI activity{' '}
                    <span>
                      ·{' '}
                      {state.model.startsWith('@cf/')
                        ? 'Cloudflare model'
                        : state.model.startsWith('gpt-')
                          ? 'OpenAI model'
                          : 'local model'}
                    </span>
                  </summary>
                  <ul>
                    {state.run?.trace.map((item, index) => (
                      <li key={index}>
                        {item.detail}
                        {item.tokens > 0
                          ? ' · ' +
                            item.tokens +
                            ' tokens · ' +
                            (item.milliseconds / 1000).toFixed(1) +
                            's'
                          : ''}
                      </li>
                    ))}
                  </ul>
                </details>
              )}
              {state.page === 'tasks' && state.selected === 'intro' ? (
                <article className="field-card task-intro">
                  <h2 ref={mainHeading} tabIndex={-1}>
                    Onboarding Task List
                  </h2>
                  <p>
                    Complete the remaining tasks, then submit this page for review. AI can fill
                    information from your documents; you confirm the actions.
                  </p>
                  <button
                    type="button"
                    className="primary"
                    onClick={() => navigate('tasks', taskFields[0].id, true)}
                  >
                    CONTINUE
                    <ChevronRight size={14} />
                  </button>
                </article>
              ) : (
                <>
                  <div className="section-heading">
                    <h2 ref={mainHeading} tabIndex={-1}>
                      {state.selected === 'summary'
                        ? 'Page Summary'
                        : state.page === 'general'
                          ? 'General Information'
                          : selected?.group}
                    </h2>
                    <div className="section-controls">
                      <button
                        type="button"
                        aria-label="Previous item"
                        disabled={busy || state.selected === currentFields[0]?.id}
                        onClick={() =>
                          navigate(
                            state.page,
                            currentFields[
                              Math.max(
                                0,
                                currentFields.findIndex((field) => field.id === state.selected) - 1,
                              )
                            ].id,
                          )
                        }
                      >
                        <ArrowUp size={16} />
                      </button>
                      <button
                        type="button"
                        aria-label="Next item"
                        disabled={busy || state.selected === 'summary'}
                        onClick={nextField}
                      >
                        <ArrowDown size={16} />
                      </button>
                      <button
                        type="button"
                        aria-label="Page summary"
                        disabled={busy}
                        onClick={() => navigate(state.page, 'summary')}
                      >
                        <Home size={17} />
                      </button>
                    </div>
                  </div>
                  {state.selected === 'summary' ? (
                    <article className="field-card page-summary">
                      <div className="summary-numbers">
                        <div>
                          <strong>{currentFields.length}</strong>
                          <span>Total</span>
                        </div>
                        <div>
                          <strong>{submittedCount(state.page)}</strong>
                          <span>Submitted</span>
                        </div>
                        <div>
                          <strong>
                            {Math.round((submittedCount(state.page) / currentFields.length) * 100)}%
                          </strong>
                          <span>Submitted</span>
                        </div>
                      </div>
                      <h2>
                        {complete
                          ? 'Your request is ready for review'
                          : submittedCount(state.page) === currentFields.length
                            ? 'This page has been submitted'
                            : 'Review the answers on this page'}
                      </h2>
                      <p>
                        {complete
                          ? 'All 23 items are submitted. Nothing has been approved automatically.'
                          : 'Drafts are saved. Submit the page when you have checked the information.'}
                      </p>
                      <h3>
                        {submittedCount(state.page) === currentFields.length
                          ? 'Submitted answers'
                          : 'Outstanding items'}
                      </h3>
                      <ul className="summary-items">
                        {currentFields
                          .filter(
                            (field) =>
                              submittedCount(state.page) === currentFields.length ||
                              !isSubmitted(state.answers[field.id]),
                          )
                          .map((field) => (
                            <li key={field.id}>
                              <button
                                type="button"
                                onClick={() => navigate(state.page, field.id, true)}
                              >
                                {field.label}
                              </button>
                              <span className={'status-text ' + statusFor(field)}>
                                {statusFor(field) === 'empty'
                                  ? 'Not provided'
                                  : statusFor(field) === 'submitted'
                                    ? 'Awaiting review'
                                    : statusFor(field) === 'missing'
                                      ? 'Needs information'
                                      : 'Draft'}
                              </span>
                            </li>
                          ))}
                      </ul>
                      {notice && (
                        <p className="notice" role="status">
                          {notice}
                        </p>
                      )}
                      {submittedCount(state.page) !== currentFields.length && (
                        <button type="button" className="primary" onClick={submitPage}>
                          SUBMIT PAGE FOR REVIEW
                        </button>
                      )}
                      {state.page === 'general' && (
                        <button
                          type="button"
                          className="text-link next-page"
                          onClick={() => navigate('tasks')}
                        >
                          Go to next page
                          <ArrowRight size={15} />
                        </button>
                      )}
                      {complete && (
                        <button
                          type="button"
                          className="text-link next-page"
                          onClick={requestReset}
                        >
                          Restart fictional request
                        </button>
                      )}
                    </article>
                  ) : (
                    selected && (
                      <article
                        className={'field-card ' + (busy ? 'ai-filling' : '')}
                        key={selected.id}
                        aria-label={selected.label}
                      >
                        <div className="field-title">
                          <h3>
                            <label htmlFor={'input-' + selected.id}>{selected.label}</label>
                          </h3>
                          {state.answers[selected.id]?.status === 'submitted' && (
                            <span className="submitted-label">Awaiting review</span>
                          )}
                        </div>
                        {selected.hint && <p className="field-hint">{selected.hint}</p>}
                        <RequestFieldInput
                          field={selected}
                          answer={state.answers[selected.id]}
                          busy={busy}
                          checks={checks}
                          setChecks={setChecks}
                          writeAnswer={writeAnswer}
                        />
                        {state.answers[selected.id]?.evidence && (
                          <button
                            type="button"
                            className="source-chip"
                            onClick={() =>
                              inspectSource(state.answers[selected.id].evidence!, selected.label)
                            }
                          >
                            <FileText size={13} />
                            {documents[state.answers[selected.id].evidence!.document].name} · p.{' '}
                            {documents[state.answers[selected.id].evidence!.document].page}
                            <span>View source</span>
                          </button>
                        )}
                        {state.answers[selected.id]?.author === 'you' && (
                          <p className="edited-label">Entered by you</p>
                        )}
                        {selected.id === state.run?.question?.fieldId &&
                          state.ai === 'waiting' &&
                          !isAnswered(state.answers[selected.id]) && (
                            <div className="clarification">
                              <p>
                                <strong>Not found in the client documents.</strong>
                              </p>
                              <p>{state.run?.question?.text}</p>
                              {state.replyReady ? (
                                <>
                                  <blockquote>{taxReply.evidence.quote}</blockquote>
                                  <button type="button" className="primary" onClick={resume}>
                                    CONTINUE AI FILLING
                                  </button>
                                </>
                              ) : (
                                <button
                                  type="button"
                                  className="outline"
                                  onClick={() => void supplyReply()}
                                >
                                  USE SAMPLE CLIENT REPLY
                                </button>
                              )}
                            </div>
                          )}
                        {notice && (
                          <p className="notice" role="alert">
                            {notice}
                          </p>
                        )}
                        <div className="field-actions">
                          {state.answers[selected.id]?.status === 'submitted' ? (
                            <button
                              type="button"
                              className="outline"
                              onClick={() =>
                                writeAnswer(selected.id, state.answers[selected.id].value)
                              }
                            >
                              EDIT ANSWER
                            </button>
                          ) : (
                            <>
                              <button
                                type="button"
                                className="primary"
                                disabled={busy}
                                onClick={() => submit(selected)}
                              >
                                SUBMIT FOR REVIEW
                              </button>
                              <span className="or">or</span>
                              <button
                                type="button"
                                className="text-link"
                                disabled={busy}
                                onClick={nextField}
                              >
                                Save draft and continue
                              </button>
                            </>
                          )}
                          <button
                            type="button"
                            className="outline ask-question"
                            disabled={busy}
                            onClick={() => setQuestionOpen(!questionOpen)}
                          >
                            <MessageCircle size={13} />
                            ASK A QUESTION
                          </button>
                        </div>
                        {questionOpen && (
                          <div className="question-box">
                            <label htmlFor="question">Question about this item</label>
                            <textarea
                              id="question"
                              value={question}
                              rows={3}
                              onChange={(event) => setQuestion(event.target.value)}
                            />
                            <button
                              type="button"
                              className="outline"
                              disabled={!question.trim()}
                              onClick={() => void saveQuestion()}
                            >
                              SAVE QUESTION
                            </button>
                          </div>
                        )}
                        {state.questions[selected.id] && (
                          <blockquote>{state.questions[selected.id]}</blockquote>
                        )}
                        {message && (
                          <p className="notice" role="status">
                            {message}
                          </p>
                        )}
                      </article>
                    )
                  )}
                </>
              )}
            </>
          )}
        </div>
      </main>
      <dialog
        ref={resetDialog}
        className="reset-dialog"
        aria-labelledby="reset-title"
        aria-describedby="reset-description"
        onCancel={(event) => {
          if (resetting) event.preventDefault();
        }}
      >
        <h2 id="reset-title">Reset demo?</h2>
        <p id="reset-description">
          This clears your demo answers, questions and submissions and returns you to Instructions.
        </p>
        <div className="reset-actions">
          <button
            type="button"
            className="outline"
            autoFocus
            disabled={resetting}
            onClick={() => resetDialog.current?.close()}
          >
            Cancel
          </button>
          <button
            type="button"
            className="primary"
            disabled={resetting}
            onClick={() => void reset()}
          >
            {resetting ? 'Resetting…' : 'Reset demo'}
          </button>
        </div>
      </dialog>
      <SourceDocument
        source={source}
        sourceField={sourceField}
        replyReady={state.replyReady}
        inspectSource={inspectSource}
        onClose={() => setSource(null)}
      />
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {busy
          ? fillingLabel
          : state.ai === 'waiting'
            ? 'AI paused. Tax reference missing. Four completion actions remain for you.'
            : notice}
      </div>
    </div>
  );
}
