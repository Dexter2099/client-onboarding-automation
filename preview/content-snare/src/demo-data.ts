import seed from '../../../src/OnboardFlow.Api/ClientRequests/demo.json';
export type Page = 'instructions' | 'general' | 'tasks';
export type SourceId = 'profile' | 'setup' | 'reply';
export type FieldKind =
  'text' | 'email' | 'tel' | 'url' | 'date' | 'long' | 'choice' | 'attachment' | 'confirmation';
export interface Evidence {
  document: SourceId;
  section: string;
  quote: string;
}
export interface RequestedField {
  id: string;
  label: string;
  page: 'general' | 'tasks';
  group: string;
  kind: FieldKind;
  hint?: string;
  options?: string[];
  sample?: string;
  confirmation?: string[];
}
export const documents = seed.documents as Record<
  SourceId,
  { name: string; page: number; sections: { id: string; title: string; text: string }[] }
>;
export const fields = seed.fields as RequestedField[];
export const generalFields = fields.filter((field) => field.page === 'general');
export const taskFields = fields.filter((field) => field.page === 'tasks');
export const taxReply = {
  evidence: { document: 'reply' as const, section: 'tax', quote: documents.reply.sections[0].text },
};
export const fieldList = (page: Page): RequestedField[] =>
  page === 'general' ? generalFields : page === 'tasks' ? taskFields : [];
