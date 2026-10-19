export type Answer = {
  goal: string | null;
  audience: string | null;
  pages: string[];
  assetReferences: string[];
};
export type Revision = {
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
export type Item = {
  id: string;
  kind: string;
  label: string;
  state: string;
  validation: string | null;
  revisions: Revision[];
};
export type Brief = {
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
export type Workflow = {
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

export const formatWorkflowDate = (value: string) =>
  new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
