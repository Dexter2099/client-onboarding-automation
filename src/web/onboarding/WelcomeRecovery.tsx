import { formatWorkflowDate as date, type Workflow } from './workflow';

export function WelcomeRecovery({
  onboarding,
  blocked,
  onRestore,
  onTryWelcome,
}: {
  onboarding: NonNullable<Workflow['onboarding']>;
  blocked: boolean;
  onRestore: () => void;
  onTryWelcome: () => void;
}) {
  const attempts = onboarding.welcomeAttempts;
  return (
    <section className="welcome-recovery" aria-label="Welcome recovery">
      <h3>{attempts.length >= 3 ? 'Welcome attempts exhausted' : 'Welcome not prepared'}</h3>
      <p>{attempts.length} of 3 attempts used · Request and review task saved</p>
      <p>
        {attempts.length >= 3
          ? 'Your work is retained. Reset both sample projects below for a fresh sandbox.'
          : onboarding.welcomeServiceAvailable
            ? 'The simulated service is available. Try welcome to continue.'
            : 'Restore the simulated service, then try the welcome again.'}
      </p>
      <div className="recovery-actions">
        <button
          className={onboarding.welcomeServiceAvailable ? 'button secondary' : 'button primary'}
          disabled={blocked || onboarding.welcomeServiceAvailable}
          onClick={onRestore}
        >
          {onboarding.welcomeServiceAvailable
            ? 'Simulated service available'
            : 'Restore simulated service'}
        </button>
        <button
          className={onboarding.welcomeServiceAvailable ? 'button primary' : 'button secondary'}
          disabled={blocked || attempts.length >= 3}
          onClick={onTryWelcome}
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
  );
}
