import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../api';
import type { Workflow } from './workflow';

export function useOnboardingWorkflow(
  projectId: string,
  onSessionUnavailable: (error: unknown) => void,
  onActionSuccess: (action: string, payload: unknown) => void,
  onInitialLoad: (workflow: Workflow) => void,
) {
  const [data, setData] = useState<Workflow | null>(null);
  const [busy, setBusy] = useState(true);
  const [needsReload, setNeedsReload] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const alive = useRef(false);
  const path = `/projects/${projectId}`;

  function report(error: unknown) {
    if (!alive.current) return;
    if (error instanceof ApiError && error.status === 401) onSessionUnavailable(error);
    else setError(error instanceof Error ? error.message : 'Could not load onboarding.');
  }

  useEffect(() => {
    let cancelled = false;
    alive.current = true;
    api<Workflow>(`${path}/onboarding`)
      .then((result) => {
        if (cancelled) return;
        setData(result);
        onInitialLoad(result);
      })
      .catch((error) => {
        if (!cancelled) report(error);
      })
      .finally(() => {
        if (!cancelled) setBusy(false);
      });
    return () => {
      cancelled = true;
      alive.current = false;
    };
  }, [projectId]);

  async function loadSaved() {
    setBusy(true);
    setError('');
    try {
      const result = await api<Workflow>(`${path}/onboarding`);
      if (alive.current) {
        setData(result);
        setNeedsReload(false);
      }
    } catch (error) {
      report(error);
    } finally {
      if (alive.current) setBusy(false);
    }
  }

  async function perform(action: string, payload: unknown, message: string) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await api<Workflow>(`${path}/${action}`, payload);
      if (!alive.current) return;
      setData(result);
      setNotice(
        (action === 'start' && !data?.onboarding) || action === 'welcome'
          ? result.onboarding?.welcomePreparedAt
            ? 'Welcome prepared in the simulated Outbox.'
            : 'Welcome unavailable. Your request and review task are saved.'
          : message,
      );
      onActionSuccess(action, payload);
    } catch (error) {
      report(error);
      // An uncertain response requires a read before any new command can spend another attempt.
      setNeedsReload(!(error instanceof ApiError) || error.status >= 500);
      if (alive.current && error instanceof ApiError && error.status === 409) {
        try {
          const saved = await api<Workflow>(`${path}/onboarding`);
          if (alive.current) {
            setData(saved);
            setNeedsReload(false);
          }
        } catch (reloadError) {
          report(reloadError);
          setNeedsReload(true);
        }
      }
    } finally {
      if (alive.current) setBusy(false);
    }
  }

  return { data, busy, needsReload, error, notice, setError, loadSaved, perform };
}
