import { useEffect, useRef } from 'react';
import { X } from 'lucide-react';
import { documents } from './demo-data';
import type { Evidence } from './demo-data';

interface Props {
  source: Evidence | null;
  sourceField: string | null;
  replyReady: boolean;
  inspectSource: (value: Evidence, fieldName?: string) => void;
  onClose: () => void;
}

export function SourceDocument({ source, sourceField, replyReady, inspectSource, onClose }: Props) {
  const sourceDialog = useRef<HTMLDialogElement>(null);
  const activeSource = source ? documents[source.document] : null;
  useEffect(() => {
    // Native modal behaviour retains Escape handling, focus containment and return focus.
    if (source && !sourceDialog.current?.open) sourceDialog.current?.showModal();
  }, [source]);
  return (
    <dialog
      ref={sourceDialog}
      className="source-drawer"
      aria-label="Client source document"
      onCancel={() => onClose()}
      onClose={() => onClose()}
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
            onClose();
          }}
        >
          <X size={21} />
        </button>
      </div>
      <nav className="source-documents" aria-label="Documents in client pack">
        {(['profile', 'setup', ...(replyReady ? ['reply'] : [])] as const).map((id) => {
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
  );
}
