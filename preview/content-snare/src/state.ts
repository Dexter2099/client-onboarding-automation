import type { Evidence, Page } from './demo-data';
import type { RequestSnapshot } from './api';
export interface Answer {
  value: string;
  status: 'draft' | 'submitted';
  author: 'ai' | 'you';
  evidence?: Evidence;
  revision?: number;
}
export interface DemoState {
  version: number;
  answers: Record<string, Answer>;
  questions: Record<string, string>;
  ai: 'idle' | 'filling' | 'waiting' | 'complete' | 'failed' | 'interrupted';
  replyReady: boolean;
  run: RequestSnapshot['run'];
  model: string;
  page: Page;
  selected: string;
}
export const initialState = (): DemoState => ({
  version: 0,
  answers: {},
  questions: {},
  ai: 'idle',
  replyReady: false,
  run: null,
  model: '',
  page: 'instructions',
  selected: 'intro',
});
export const isAnswered = (answer?: Answer) => !!answer?.value.trim();
export const isSubmitted = (answer?: Answer) =>
  isAnswered(answer) && answer?.status === 'submitted';
