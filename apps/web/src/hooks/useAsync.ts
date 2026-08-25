import { useCallback, useEffect, useState } from 'react';

export function useAsync<T>(load: () => Promise<T>, deps: readonly unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [loading, setLoading] = useState(true);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      setData(await load());
    } catch (caught) {
      setError(caught instanceof Error ? caught : new Error('Unknown error'));
    } finally {
      setLoading(false);
    }
  }, deps);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { data, error, loading, reload };
}
