import { FileText } from 'lucide-react';
import type { RequestedField } from './demo-data';
import type { Answer } from './state';

interface Props {
  field: RequestedField;
  answer?: Answer;
  busy: boolean;
  checks: string[];
  setChecks: (checks: string[]) => void;
  writeAnswer: (id: string, value: string) => void;
}

export function RequestFieldInput({ field, answer, busy, checks, setChecks, writeAnswer }: Props) {
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
