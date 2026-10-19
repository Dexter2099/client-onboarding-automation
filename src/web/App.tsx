import { useEffect, useRef, useState, type ReactNode } from 'react';
import { ArrowLeft, ArrowRight, RotateCcw, TriangleAlert } from 'lucide-react';
import { api, ApiError, type Project } from './api';
import { OnboardingPanel } from './OnboardingPanel';

type Session = { id: string; createdAt: string; expiresAt: string };
type Workspace = { session: Session; projects: Project[] };

// Share the initial request across React StrictMode's development effect replay.
let initialLoad: Promise<Workspace> | undefined;
async function loadWorkspace(createWhenMissing = false): Promise<Workspace> {
  let session: Session;
  try {
    session = await api<Session>('/session');
  } catch (error) {
    if (!(createWhenMissing && error instanceof ApiError && error.code === 'sandbox_missing'))
      throw error;
    session = await api<Session>('/session', {});
  }
  const projects = await api<Project[]>('/projects');
  return { session, projects };
}

function Button({
  children,
  onClick,
  disabled = false,
  secondary = false,
}: {
  children: ReactNode;
  onClick: () => void;
  disabled?: boolean;
  secondary?: boolean;
}) {
  return (
    <button
      className={`button ${secondary ? 'secondary' : 'primary'}`}
      onClick={onClick}
      disabled={disabled}
    >
      {children}
    </button>
  );
}

export function App() {
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [selected, setSelected] = useState('OF-1042');
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState('');
  const [unavailable, setUnavailable] = useState(false);
  const [notice, setNotice] = useState('');
  const generation = useRef(0);
  const checkedExpiry = useRef('');
  const projectHeading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    projectHeading.current?.focus();
  }, [selected]);

  function showError(error: unknown) {
    const expired = error instanceof ApiError && error.status === 401;
    setError(error instanceof Error ? error.message : 'Could not load the workspace.');
    setUnavailable(expired);
    if (expired) setWorkspace(null);
  }

  useEffect(() => {
    let cancelled = false;
    initialLoad ??= loadWorkspace(true);
    initialLoad
      .then((result) => {
        if (!cancelled) setWorkspace(result);
      })
      .catch((error: unknown) => {
        if (!cancelled) showError(error);
      })
      .finally(() => {
        if (!cancelled) setBusy(false);
        initialLoad = undefined;
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!workspace || checkedExpiry.current === workspace.session.expiresAt) return;
    const remaining = new Date(workspace.session.expiresAt).getTime() - Date.now();
    const timeout = window.setTimeout(
      () => {
        checkedExpiry.current = workspace.session.expiresAt;
        void reload(); // The server decides whether access has expired.
      },
      Math.max(0, remaining) + 100,
    );
    return () => window.clearTimeout(timeout);
  }, [workspace]);

  async function reload() {
    const version = ++generation.current;
    setBusy(true);
    setError('');
    try {
      const result = await loadWorkspace();
      if (version === generation.current) setWorkspace(result);
    } catch (error) {
      if (version === generation.current) showError(error);
    } finally {
      if (version === generation.current) setBusy(false);
    }
  }

  async function replaceSandbox(fresh: boolean) {
    const version = ++generation.current;
    setBusy(true);
    setError('');
    setNotice('');
    setWorkspace(null);
    try {
      await api<Session>(fresh ? '/session' : '/session/reset', {});
      const result = await loadWorkspace();
      if (version !== generation.current) return;
      setWorkspace(result);
      setSelected('OF-1042');
      setUnavailable(false);
      setNotice(
        fresh ? 'A fresh 24-hour sandbox is ready.' : 'Both sample projects have been reset.',
      );
    } catch (error) {
      if (version === generation.current) showError(error);
    } finally {
      if (version === generation.current) setBusy(false);
    }
  }

  const project = workspace?.projects.find((item) => item.reference === selected);
  const recovery = selected === 'OF-1043';
  const expiry =
    workspace &&
    new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(workspace.session.expiresAt));

  return (
    <div className="site-shell">
      <header className="site-header">
        <a className="brand" href="/" aria-label="OnboardFlow home">
          <span className="brand-mark" aria-hidden="true">
            <i />
            <i />
            <i />
            <i />
          </span>
          OnboardFlow
        </a>
        <span className="environment-label">Interactive demo · Fictional data</span>
      </header>
      <main id="main-content" tabIndex={-1}>
        <div className="intro">
          <h1>{recovery ? 'Recover a failed welcome' : 'Get Juniper ready for kickoff'}</h1>
          <p>
            {recovery
              ? "You're the project manager, recovering a failed welcome on a separate sample project. Start onboarding to see the failure, restore the simulated service, then try welcome again. Your main walkthrough stays saved."
              : "You're the project manager, getting Juniper Studio's signed website redesign ready for kickoff. Review Maya's replies, resolve the extra Shop page, then approve the brief and publish a simulated handoff."}
          </p>
        </div>

        <div role="status" aria-live="polite" className="status-line">
          {busy ? 'Loading your private workspace…' : notice}
        </div>
        {error && (
          <div className="error-panel" role="alert">
            <TriangleAlert size={20} aria-hidden="true" />
            <div>
              <p>{error}</p>
              {unavailable ? (
                <Button onClick={() => void replaceSandbox(true)} disabled={busy}>
                  Start fresh <ArrowRight size={16} aria-hidden="true" />
                </Button>
              ) : (
                <Button onClick={() => void reload()} disabled={busy} secondary>
                  Load saved workspace
                </Button>
              )}
            </div>
          </div>
        )}

        {workspace && (
          <>
            {recovery && (
              <nav className="walkthrough-navigation" aria-label="Walkthrough">
                <Button onClick={() => setSelected('OF-1042')} secondary>
                  <ArrowLeft size={16} aria-hidden="true" />
                  Back to main walkthrough
                </Button>
              </nav>
            )}
            {project && (
              <section className="project-card" aria-labelledby="project-heading">
                <div className="project-heading">
                  <img src="/clients/juniper.webp" alt="" width="48" height="48" />
                  <div>
                    <p className="eyebrow">
                      {recovery && 'Optional failure recovery · '}
                      {project.reference} · {project.clientName}
                    </p>
                    <h2 id="project-heading" ref={projectHeading} tabIndex={-1}>
                      {project.name}
                    </h2>
                  </div>
                </div>
                <details className="project-context" key={`context-${project.id}`}>
                  <summary>
                    Project details <span>Signed five-page website</span>
                  </summary>
                  <dl className="project-details">
                    <div>
                      <dt>Project manager</dt>
                      <dd>{project.owner}</dd>
                    </div>
                    <div>
                      <dt>Client contact</dt>
                      <dd>{project.clientContact}</dd>
                    </div>
                    <div>
                      <dt>Service</dt>
                      <dd>{project.service}</dd>
                    </div>
                  </dl>
                  <div className="signed-scope">
                    <h3>Agreed website pages</h3>
                    <ul>
                      {project.signedPages.map((page) => (
                        <li key={page}>{page}</li>
                      ))}
                    </ul>
                  </div>
                </details>
                <OnboardingPanel
                  key={project.id}
                  project={project}
                  onSessionUnavailable={showError}
                />
              </section>
            )}
            {!recovery && (
              <aside className="optional-exercise" aria-labelledby="recovery-heading">
                <div>
                  <h2 id="recovery-heading">Optional: failure recovery</h2>
                  <p>
                    See how a failed welcome keeps the request and task saved. This uses a separate
                    sample project; your main walkthrough stays saved.
                  </p>
                </div>
                <Button onClick={() => setSelected('OF-1043')} secondary>
                  Try failure recovery <ArrowRight size={16} aria-hidden="true" />
                </Button>
              </aside>
            )}
            <aside className="demo-tools" aria-labelledby="demo-heading">
              <div>
                <h2 id="demo-heading">Your private demo</h2>
                <p>Work saves on refresh. Expires {expiry} — 24 hours from creation.</p>
                <p>Reset clears both projects for you only.</p>
              </div>
              <Button onClick={() => void replaceSandbox(false)} disabled={busy} secondary>
                <RotateCcw size={16} aria-hidden="true" />
                Reset both sample projects
              </Button>
            </aside>
          </>
        )}
      </main>
      <footer>A personal project by Dexter Mehta · Simulated email and handoff</footer>
    </div>
  );
}
