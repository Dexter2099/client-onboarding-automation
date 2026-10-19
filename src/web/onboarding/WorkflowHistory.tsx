import { formatWorkflowDate as date, type Workflow } from './workflow';

export function ActivityLog({ events }: { events: Workflow['activity'] }) {
  return (
    <section aria-label="Project activity">
      <h3>Activity</h3>
      {events.length === 0 ? (
        <p className="empty-state">No project activity yet.</p>
      ) : (
        <ol className="activity-list">
          {events.map((event) => (
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
  );
}

export function SimulatedOutbox({ messages }: { messages: Workflow['outbox'] }) {
  return (
    <section aria-label="Simulated outbox">
      <h3>Outbox</h3>
      <p className="muted">Simulated messages, prepared in this sandbox. No email is sent.</p>
      {messages.length === 0 ? (
        <p className="empty-state">No messages prepared yet.</p>
      ) : (
        messages.map((message) => (
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
  );
}
