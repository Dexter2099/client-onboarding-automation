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
export class IntakeRequestCancelled extends Error {}

export class IntakeApi {
  private csrf = '';
  private currentVersion = 0;
  private start: Promise<RequestSnapshot> | null = null;
  private queue: Promise<unknown> = Promise.resolve();
  private pendingEdit: {
    fields: { field?: string; value?: string; page?: string };
    result: Promise<RequestSnapshot>;
  } | null = null;
  private generation = 0;
  private lifetime = new AbortController();
  private assertCurrent(generation: number) {
    if (generation !== this.generation) throw new IntakeRequestCancelled();
  }
  private async request(
    path: string,
    body?: unknown,
    generation = this.generation,
    deadline?: AbortSignal,
  ): Promise<unknown> {
    this.assertCurrent(generation);
    // AI has its existing server deadline; ordinary reads/writes must not hang forever.
    const timeout = deadline ?? (path === 'intake/agent' ? undefined : AbortSignal.timeout(15000));
    const signal = timeout
      ? AbortSignal.any([this.lifetime.signal, timeout])
      : this.lifetime.signal;
    try {
      const response = await fetch('/api/' + path, {
        method: body === undefined ? 'GET' : 'POST',
        credentials: 'same-origin',
        headers:
          body === undefined
            ? {}
            : { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': this.csrf },
        ...(body === undefined ? {} : { body: JSON.stringify(body) }),
        signal,
      });
      const data = await response.json();
      this.assertCurrent(generation);
      if (!response.ok) throw new Error(data.title || 'Load saved work before trying again.');
      return data;
    } catch (error) {
      this.assertCurrent(generation);
      if (signal.reason?.name === 'TimeoutError')
        throw new Error('The request took too long. Load saved work before trying again.');
      throw error;
    }
  }
  private accept(value: unknown): RequestSnapshot {
    const snapshot = value as RequestSnapshot;
    this.currentVersion = Math.max(this.currentVersion, snapshot.version);
    return snapshot;
  }
  initialize(): Promise<RequestSnapshot> {
    const generation = this.generation;
    return (this.start ??= (async () => {
      const deadline = AbortSignal.timeout(15000);
      try {
        const [protection, response] = await Promise.all([
          this.request('csrf', undefined, generation, deadline) as Promise<{ token: string }>,
          fetch('/api/intake/', {
            credentials: 'same-origin',
            signal: AbortSignal.any([this.lifetime.signal, deadline]),
          }),
        ]);
        this.assertCurrent(generation);
        this.csrf = protection.token;
        const snapshot = await response.json();
        this.assertCurrent(generation);
        if (!response.ok) {
          if (response.status !== 401)
            throw new Error(snapshot.title || 'Load saved work before trying again.');
          if (snapshot.code !== 'sandbox_missing')
            throw new Error('This sandbox expired. Start a fresh workspace.');
          const created = (await this.request('session', {}, generation)) as {
            intake?: RequestSnapshot;
          };
          // A concurrent start can reuse an existing owner; read its current request in that case.
          return this.accept(
            created.intake ?? (await this.request('intake/', undefined, generation)),
          );
        }
        return this.accept(snapshot);
      } catch (error) {
        this.assertCurrent(generation);
        if (deadline.aborted)
          throw new Error('The request took too long. Load saved work before trying again.');
        throw error;
      }
    })());
  }
  async read(): Promise<RequestSnapshot> {
    const generation = this.generation;
    await this.initialize();
    return this.accept(await this.request('intake/', undefined, generation));
  }
  command(
    operation: string,
    fields: { field?: string; value?: string; page?: string } = {},
  ): Promise<RequestSnapshot> {
    const generation = this.generation;
    if (operation === 'edit' && fields.field && this.pendingEdit?.fields.field === fields.field) {
      // Only combine adjacent, unsent edits. A different field/action remains an ordering barrier.
      Object.assign(this.pendingEdit.fields, fields);
      return this.pendingEdit.result;
    }
    const pendingFields = { ...fields };
    const task = this.queue.then(async () => {
      if (this.pendingEdit?.fields === pendingFields) this.pendingEdit = null;
      this.assertCurrent(generation);
      await this.initialize();
      return this.accept(
        await this.request(
          'intake/command',
          {
            commandId: crypto.randomUUID(),
            expectedVersion: this.currentVersion,
            operation,
            ...pendingFields,
          },
          generation,
        ),
      );
    });
    this.queue = task;
    this.pendingEdit = operation === 'edit' ? { fields: pendingFields, result: task } : null;
    return task;
  }
  async reload(): Promise<RequestSnapshot> {
    // Recovery must discard queued writes and late results, just like reset, without clearing saved work.
    this.generation++;
    this.lifetime.abort();
    this.lifetime = new AbortController();
    this.pendingEdit = null;
    this.queue = Promise.resolve();
    this.start = null;
    this.currentVersion = 0;
    const operation = this.initialize();
    this.queue = operation;
    return operation;
  }
  async fresh(): Promise<RequestSnapshot> {
    this.pendingEdit = null;
    const generation = this.generation;
    const protection = (await this.request('csrf', undefined, generation)) as { token: string };
    this.csrf = protection.token;
    const created = (await this.request('session', {}, generation)) as { intake?: RequestSnapshot };
    this.currentVersion = 0;
    this.queue = Promise.resolve();
    const snapshot = this.accept(
      created.intake ?? (await this.request('intake/', undefined, generation)),
    );
    this.start = Promise.resolve(snapshot);
    return snapshot;
  }
  async agent(resume: boolean): Promise<RequestSnapshot> {
    this.pendingEdit = null;
    const generation = this.generation;
    await this.queue;
    this.assertCurrent(generation);
    await this.initialize();
    return this.accept(
      await this.request(
        'intake/agent',
        {
          commandId: crypto.randomUUID(),
          expectedVersion: this.currentVersion,
          resume,
        },
        generation,
      ),
    );
  }
  reset(): Promise<RequestSnapshot> {
    // Discard queued work and reject late responses before they can restore cleared answers.
    this.generation++;
    this.pendingEdit = null;
    this.lifetime.abort();
    this.lifetime = new AbortController();
    this.currentVersion = 0;
    const generation = this.generation;
    const deadline = AbortSignal.timeout(15000);
    const operation = (async () => {
      if (!this.csrf) {
        const protection = (await this.request('csrf', undefined, generation, deadline)) as {
          token: string;
        };
        this.csrf = protection.token;
      }
      // This existing endpoint preserves an active owner and creates one only if missing/expired.
      await this.request('session', {}, generation, deadline);
      return this.accept(
        await this.request(
          'intake/command',
          {
            commandId: crypto.randomUUID(),
            expectedVersion: 0,
            operation: 'reset',
          },
          generation,
          deadline,
        ),
      );
    })();
    this.start = operation;
    this.queue = operation;
    return operation;
  }
}
