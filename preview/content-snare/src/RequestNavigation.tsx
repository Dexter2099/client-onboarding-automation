import { Check, ChevronDown, ChevronRight } from 'lucide-react';
import { generalFields, taskFields } from './demo-data';
import type { Page, RequestedField } from './demo-data';
import { isSubmitted } from './state';
import type { DemoState } from './state';

interface Props {
  state: Pick<DemoState, 'page' | 'selected' | 'answers'>;
  mobileNav: boolean;
  busy: boolean;
  complete: boolean;
  navigate: (page: Page, id?: string, focus?: boolean) => void;
  statusFor: (field: RequestedField) => string;
  submittedCount: (page: Page) => number;
}
const groupNames = [...new Set(taskFields.map((field) => field.group))];

// Navigation renders the saved request status; the parent owns commands and focus.
export function RequestNavigation({
  state,
  mobileNav,
  busy,
  complete,
  navigate,
  statusFor,
  submittedCount,
}: Props) {
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

  return (
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
  );
}
