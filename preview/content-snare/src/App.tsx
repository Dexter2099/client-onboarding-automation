import { useEffect, useRef, useState } from 'react';
import {
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  Check,
  ChevronDown,
  ChevronRight,
  FileText,
  Home,
  Menu,
  MessageCircle,
  Play,
  Sparkles,
  X,
} from 'lucide-react';
import { documents, fieldList, fields, generalFields, taskFields, taxReply } from './demo-data';
import type { Evidence, Page, RequestedField } from './demo-data';
import { initialState, isAnswered, isSubmitted } from './state';
import { IntakeApi } from './api';
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
  const [walkthroughPlaying, setWalkthroughPlaying] = useState(false);
  const [questionOpen, setQuestionOpen] = useState(false);
  const [question, setQuestion] = useState('');
  const [message, setMessage] = useState('');
  const [mobileNav, setMobileNav] = useState(false);
  const [fillingLabel, setFillingLabel] = useState('');
  const [traceOpen, setTraceOpen] = useState(false);
  const [notice, setNotice] = useState('');
  const [checks, setChecks] = useState<string[]>([]);
  const sourceDialog = useRef<HTMLDialogElement>(null);
  const fillGeneration = useRef(0);
  const mainHeading = useRef<HTMLHeadingElement>(null);
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
        if (current) setApiError(error.message);
      });
    return () => {
      current = false;
    };
  }, []);
  useEffect(() => {
    setWalkthroughPlaying(false);
  }, [state.page]);
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
    if (source && !sourceDialog.current?.open) sourceDialog.current?.showModal();
  }, [source]);
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
      snapshot.version < previous.version ? previous : { ...previous, ...snapshot },
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
      setApiError((error as Error).message);
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
      .catch((error) => setApiError(error.message));
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
      setApiError((error as Error).message);
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
      setApiError((error as Error).message);
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
    const timer = setInterval(async () => {
      if (polling) return;
      polling = true;
      try {
        const snapshot = await api.current.read();
        if (generation === fillGeneration.current) {
          applySnapshot(snapshot);
          const ids = Object.keys(snapshot.answers).filter((id) => !state.answers[id]);
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
      if (generation !== fillGeneration.current) return;
      applySnapshot(snapshot);
      const pending = fields.find((field) => field.id === snapshot.run?.question?.fieldId);
      if (pending) navigate(pending.page, pending.id, true);
      else if (snapshot.ai === 'complete') navigate('general', 'summary', true);
    } catch (error) {
      try {
        applySnapshot(await api.current.reload());
      } catch {
        /* Keep the original failure visible. */
      }
      setApiError((error as Error).message);
    } finally {
      clearInterval(timer);
      activeRequest.current = false;
    }
  }
  const fillFromDocument = () => runAgent(false);
  const resume = () => runAgent(true);
  async function supplyReply() {
    try {
      applySnapshot(await api.current.command('reply'));
    } catch (error) {
      setApiError((error as Error).message);
    }
  }
  async function saveQuestion() {
    if (!selected) return;
    try {
      applySnapshot(
        await api.current.command('question', { field: selected.id, value: question.trim() }),
      );
      setMessage('Question saved in this local preview. No message was sent.');
      setQuestionOpen(false);
      setQuestion('');
    } catch (error) {
      setApiError((error as Error).message);
    }
  }
  async function reset() {
    try {
      const snapshot = await api.current.command('reset');
      applySnapshot(snapshot);
      navigate('instructions');
      setSource(null);
      setNotice('');
      setApiError('');
    } catch (error) {
      setApiError((error as Error).message);
    }
  }
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
  const groupNames = [...new Set(taskFields.map((field) => field.group))];

  function fieldNavigation(list: RequestedField[], number: string) {
    return (
      <ul className="field-tree">
        {list.map((field, index) => (
          <li key={field.id}>
            <button
              type="button"
              className={'field-link ' + (state.selected === field.id ? 'selected' : '')}
              aria-current={state.selected === field.id ? 'step' : undefined}
              onClick={() => navigate(field.page, field.id)}
              disabled={busy}
            >
              <span className="tree-branch" aria-hidden="true" />
              <span className="field-name" title={field.label}>
                {field.label}
              </span>
              <span className={'item-circle ' + statusFor(field)} aria-label={statusFor(field)}>
                {statusFor(field) === 'submitted' ? (
                  <Check size={11} />
                ) : statusFor(field) === 'draft' ? (
                  <span />
                ) : null}
              </span>
              <span className="sr-only">
                {number}.{index + 1}
              </span>
            </button>
          </li>
        ))}
      </ul>
    );
  }

  function fieldInput(field: RequestedField) {
    const answer = state.answers[field.id];
    const value = answer?.value || '';
    const disabled = busy || answer?.status === 'submitted';
    if (field.kind === 'choice') {
      return (
        <div className={field.id === 'entity' ? 'choices' : 'select-wrap'}>
          {field.id === 'entity' ? (
            field.options!.map((option) => (
              <label className="choice" key={option}>
                <input
                  type="checkbox"
                  checked={value === option}
                  disabled={disabled}
                  onChange={() => writeAnswer(field.id, value === option ? '' : option)}
                />
                {option}
              </label>
            ))
          ) : (
            <select
              id={'input-' + field.id}
              aria-label={field.label}
              value={value}
              disabled={disabled}
              onChange={(event) => writeAnswer(field.id, event.target.value)}
            >
              <option value="">Select an option...</option>
              {field.options!.map((option) => (
                <option key={option}>{option}</option>
              ))}
            </select>
          )}
        </div>
      );
    }
    if (field.kind === 'attachment')
      return (
        <div className="sample-upload">
          <FileText size={27} strokeWidth={1.5} aria-hidden="true" />
          <p>{value || 'Add a sample file'}</p>
          <p className="muted small">Fictional attachment · no real upload</p>
          <button
            className="outline"
            type="button"
            disabled={disabled}
            onClick={() => writeAnswer(field.id, field.sample!)}
          >
            Use sample attachment
          </button>
        </div>
      );
    if (field.kind === 'confirmation')
      return (
        <div className="choices">
          {field.confirmation!.map((label) => (
            <label className="choice" key={label}>
              <input
                type="checkbox"
                checked={!!value || checks.includes(label)}
                disabled={disabled}
                onChange={(event) => {
                  const next = event.target.checked
                    ? [...checks, label]
                    : checks.filter((item) => item !== label);
                  setChecks(next);
                  writeAnswer(
                    field.id,
                    next.length === field.confirmation!.length ? field.sample! : '',
                  );
                }}
              />
              {label}
            </label>
          ))}
        </div>
      );
    if (field.kind === 'long')
      return (
        <textarea
          id={'input-' + field.id}
          aria-label={field.label}
          rows={4}
          value={value}
          disabled={disabled}
          placeholder="Enter text here..."
          onChange={(event) => writeAnswer(field.id, event.target.value)}
        />
      );
    return (
      <input
        id={'input-' + field.id}
        aria-label={field.label}
        type={field.kind}
        value={value}
        disabled={disabled}
        placeholder="Enter text here..."
        onChange={(event) => writeAnswer(field.id, event.target.value)}
      />
    );
  }
  const activeSource = source ? documents[source.document] : null;
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
        <span className="status-badge">ACTIVE</span>
      </div>
      <aside
        className={'sidebar ' + (mobileNav ? 'mobile-open' : '')}
        aria-label="Client request navigation"
      >
        <div className="request-heading">
          <div className="brand" aria-label="OnboardFlow">
            <span className="brand-bracket">{'{'}</span>
            <div>
              <span>ONBOARD</span>
              <strong>FLOW</strong>
            </div>
            <span className="brand-bracket">{'}'}</span>
          </div>
          <h1>
            New Client Onboarding –<br />
            OnboardFlow Demo
          </h1>
          <div className="request-meta">
            <span>Due: 10/30/2026</span>
            <span className="status-badge">{complete ? 'IN REVIEW' : 'ACTIVE'}</span>
          </div>
        </div>
        <nav className="request-tree" aria-label="Request sections">
          <button
            type="button"
            className={'page-link ' + (state.page === 'instructions' ? 'active' : '')}
            onClick={() => navigate('instructions')}
            disabled={busy}
          >
            <span>1. Instructions</span>
          </button>
          <button
            type="button"
            className={'page-link ' + (state.page === 'general' ? 'active' : '')}
            onClick={() => navigate('general')}
            disabled={busy}
          >
            <span className="page-title">
              {state.page === 'general' ? <ChevronDown size={13} /> : <ChevronRight size={13} />}2.
              General Information
            </span>
            <span className="page-count">
              {submittedCount('general')}/11 <span className="item-circle" />
            </span>
          </button>
          {state.page === 'general' && (
            <div className="page-children">
              <div className="group-label">
                <span>2.1. General Information</span>
                <span>{submittedCount('general')}/11</span>
              </div>
              {fieldNavigation(generalFields, '2.1')}
              <button
                type="button"
                className={'summary-link ' + (state.selected === 'summary' ? 'selected' : '')}
                onClick={() => navigate('general', 'summary')}
                disabled={busy}
              >
                Page Summary
              </button>
            </div>
          )}
          <button
            type="button"
            className={'page-link ' + (state.page === 'tasks' ? 'active' : '')}
            onClick={() => navigate('tasks')}
            disabled={busy}
          >
            <span className="page-title">
              {state.page === 'tasks' ? <ChevronDown size={13} /> : <ChevronRight size={13} />}3.
              Onboarding Task List
            </span>
            <span className="page-count">
              {submittedCount('tasks')}/12 <span className="item-circle" />
            </span>
          </button>
          {state.page === 'tasks' && (
            <div className="page-children">
              {groupNames.map((group, index) => {
                const groupFields = taskFields.filter((field) => field.group === group);
                const done = groupFields.filter((field) =>
                  isSubmitted(state.answers[field.id]),
                ).length;
                return (
                  <div key={group}>
                    <button
                      type="button"
                      className="group-label group-button"
                      disabled={busy}
                      onClick={() => navigate('tasks', groupFields[0].id)}
                    >
                      <span title={group}>
                        3.{index + 1}. {group}
                      </span>
                      <span>
                        {done}/{groupFields.length}
                      </span>
                    </button>
                    {fieldNavigation(groupFields, '3.' + (index + 1))}
                  </div>
                );
              })}
              <button
                type="button"
                className={'summary-link ' + (state.selected === 'summary' ? 'selected' : '')}
                onClick={() => navigate('tasks', 'summary')}
                disabled={busy}
              >
                Page Summary
              </button>
            </div>
          )}
        </nav>
        <div className="preview-note">
          OnboardFlow · AI demo
          <br />
          <span>Fictional client data</span>
        </div>
      </aside>
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
                    setApiError((error as Error).message);
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
            <article className="welcome-card">
              <h2 ref={mainHeading} tabIndex={-1}>
                Welcome to your new client onboarding!
              </h2>
              <p>Let's get your business set up with Harbour Bookkeeping.</p>
              <p>
                We've prepared a fictional client information pack. Watch AI turn it into draft
                answers, check the sources, then submit your information for review.
              </p>
              <div className="walkthrough-video">
                {walkthroughPlaying ? (
                  <video
                    controls
                    autoPlay
                    muted
                    playsInline
                    poster="/onboardflow-local-ai-20261025.webp"
                    aria-label="Recorded walkthrough using local AI"
                  >
                    <source src="/onboardflow-local-ai-20261025.mp4" type="video/mp4" />
                    <source src="/onboardflow-local-ai-20261025.webm" type="video/webm" />
                    Your browser does not support this preview recording.
                  </video>
                ) : (
                  <button
                    type="button"
                    className="video-cover"
                    aria-label="Play walkthrough"
                    onClick={() => setWalkthroughPlaying(true)}
                  >
                    <img
                      src="/onboardflow-local-ai-20261025.webp"
                      alt="Source-linked answers in the local preview"
                    />
                    <span className="play-icon">
                      <Play size={30} fill="currentColor" />
                    </span>
                  </button>
                )}
                <span>Recorded local AI · Ollama</span>
              </div>
              <p className="intro-platform">Complete your request at your own pace.</p>
              <div className="instruction-row">
                <FileText size={25} />
                <div>
                  <strong>Your answers are saved.</strong>
                  <p>Leave and return to this preview without starting again.</p>
                </div>
              </div>
              <div className="instruction-row">
                <Sparkles size={25} />
                <div>
                  <strong>AI fills from the sample documents.</strong>
                  <p>Every AI draft links back to its supporting passage.</p>
                </div>
              </div>
              <div className="instruction-row">
                <Check size={25} />
                <div>
                  <strong>You review and submit.</strong>
                  <p>AI leaves missing information and confirmations for you.</p>
                </div>
              </div>
              <div className="instruction-legend">
                <span>
                  <i className="legend-dot draft" />
                  Draft
                </span>
                <span>
                  <i className="legend-dot submitted" />
                  Awaiting review
                </span>
                <span>
                  <i className="legend-dot missing" />
                  Needs information
                </span>
              </div>
              <button
                type="button"
                className="primary start-button"
                onClick={() => navigate('general', 'legal', true)}
              >
                LET'S GET STARTED
                <ChevronRight size={15} />
              </button>
            </article>
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
                        <button type="button" className="text-link next-page" onClick={reset}>
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
                        {fieldInput(selected)}
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
        ref={sourceDialog}
        className="source-drawer"
        aria-label="Client source document"
        onCancel={() => setSource(null)}
        onClose={() => setSource(null)}
      >
        <div className="source-toolbar">
          <div>
            <h2>{activeSource?.name}</h2>
            <p>Simulated document · provided by Maya Chen</p>
          </div>
          <button
            type="button"
            aria-label="Close source document"
            onClick={() => {
              sourceDialog.current?.close();
              setSource(null);
            }}
          >
            <X size={21} />
          </button>
        </div>
        <nav className="source-documents" aria-label="Documents in client pack">
          {(['profile', 'setup', ...(state.replyReady ? ['reply'] : [])] as const).map((id) => {
            const documentId = id as keyof typeof documents;
            const document = documents[documentId];
            const first = document.sections[0];
            return (
              <button
                type="button"
                key={id}
                aria-pressed={source?.document === id}
                onClick={() =>
                  inspectSource({ document: documentId, section: first.id, quote: first.text })
                }
              >
                {id === 'profile'
                  ? 'Company profile'
                  : id === 'setup'
                    ? 'Onboarding notes'
                    : 'Client reply'}
              </button>
            );
          })}
        </nav>
        <div className="source-paper">
          <p className="source-page">Page {activeSource?.page} · Fixture version 1</p>
          {activeSource?.sections.map((section) => (
            <section
              className={
                source?.section === section.id ? 'source-passage highlighted' : 'source-passage'
              }
              key={section.id}
            >
              <h3>{section.title}</h3>
              <p>{section.text}</p>
            </section>
          ))}
        </div>
        {source && (
          <div className="source-quote">
            <h3>{sourceField ? 'Passage used for ' + sourceField : 'Selected passage'}</h3>
            <blockquote>{source.quote}</blockquote>
          </div>
        )}
      </dialog>
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
