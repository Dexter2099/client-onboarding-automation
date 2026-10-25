import type { Answer, DemoState } from './state';

export interface RequestSnapshot {
  version: number;
  answers: Record<string, Answer>;
  questions: Record<string, string>;
  replyReady: boolean;
  ai: DemoState['ai'];
  model: string;
  run: null | {
    id: string;
    model: string;
    calls: number;
    status: string;
    error: string | null;
    question: { fieldId: string; text: string } | null;
    trace: { action: string; detail: string; tokens: number; milliseconds: number }[];
  };
}
export class IntakeApi {
  private csrf = '';
  private currentVersion = 0;
  private start: Promise<RequestSnapshot> | null = null;
  private queue: Promise<unknown> = Promise.resolve();
  private async request(path: string, body?: unknown): Promise<unknown> {
    const response = await fetch('/api/' + path, {
      method: body === undefined ? 'GET' : 'POST',
      credentials: 'same-origin',
      headers:
        body === undefined ? {} : { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': this.csrf },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.title || 'Load saved work before trying again.');
    return data;
  }
  private accept(value: unknown): RequestSnapshot {
    const snapshot = value as RequestSnapshot;
    this.currentVersion = Math.max(this.currentVersion, snapshot.version);
    return snapshot;
  }
  initialize(): Promise<RequestSnapshot> {
    return (this.start ??= (async () => {
      const protection = (await this.request('csrf')) as { token: string };
      this.csrf = protection.token;
      const response = await fetch('/api/session', { credentials: 'same-origin' });
      if (!response.ok) {
        const problem = await response.json();
        if (problem.code !== 'sandbox_missing')
          throw new Error('This sandbox expired. Start a fresh workspace.');
        await this.request('session', {});
      }
      return this.accept(await this.request('intake/'));
    })());
  }
  async read(): Promise<RequestSnapshot> {
    await this.initialize();
    return this.accept(await this.request('intake/'));
  }
  command(
    operation: string,
    fields: { field?: string; value?: string; page?: string } = {},
  ): Promise<RequestSnapshot> {
    const task = this.queue.then(async () => {
      await this.initialize();
      return this.accept(
        await this.request('intake/command', {
          commandId: crypto.randomUUID(),
          expectedVersion: this.currentVersion,
          operation,
          ...fields,
        }),
      );
    });
    this.queue = task;
    return task;
  }
  async reload(): Promise<RequestSnapshot> {
    this.queue = Promise.resolve();
    this.start = null;
    this.currentVersion = 0;
    return this.read();
  }
  async fresh(): Promise<RequestSnapshot> {
    const protection = (await this.request('csrf')) as { token: string };
    this.csrf = protection.token;
    await this.request('session', {});
    this.currentVersion = 0;
    this.queue = Promise.resolve();
    const snapshot = this.accept(await this.request('intake/'));
    this.start = Promise.resolve(snapshot);
    return snapshot;
  }
  async agent(resume: boolean): Promise<RequestSnapshot> {
    await this.queue;
    await this.initialize();
    return this.accept(
      await this.request('intake/agent', {
        commandId: crypto.randomUUID(),
        expectedVersion: this.currentVersion,
        resume,
      }),
    );
  }
}
