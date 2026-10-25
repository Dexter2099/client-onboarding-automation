export type Project = {
  id: string;
  reference: string;
  name: string;
  clientName: string;
  clientContact: string;
  owner: string;
  service: string;
  signedPages: string[];
};

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public code?: string,
  ) {
    super(message);
  }
}
export async function api<T>(path: string, body?: unknown): Promise<T> {
  let response: Response;
  try {
    let csrf: string | undefined;
    if (body !== undefined) {
      const protection = await fetch('/api/csrf', {
        credentials: 'same-origin',
        cache: 'no-store',
        signal: AbortSignal.timeout(15000),
      });
      if (!protection.ok) throw new Error('Cannot prepare the request.');
      csrf = ((await protection.json()) as { token: string }).token;
    }
    response = await fetch(`/api${path}`, {
      method: body === undefined ? 'GET' : 'POST',
      credentials: 'same-origin',
      headers:
        body === undefined ? {} : { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf! },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: AbortSignal.timeout(15000),
      cache: 'no-store',
    });
  } catch {
    throw new ApiError(
      503,
      'Cannot reach the workspace. Load saved state before trying a change again.',
    );
  }
  const data = (await response
    .json()
    .catch(() => ({ title: 'The workspace is temporarily unavailable.' }))) as {
    title?: string;
    code?: string;
  };
  if (!response.ok)
    throw new ApiError(
      response.status,
      data.title || 'Something went wrong. Please try again.',
      data.code,
    );
  return data as T;
}
