import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
  type PropsWithChildren,
} from 'react';
import { ApiError, api, clearAccessToken, getAccessToken, setAccessToken } from '../lib/api';
import type { ControlPlaneIdentity } from '../types';

type AuthContextValue = {
  identity: ControlPlaneIdentity;
  hasRole: (role: string) => boolean;
  signOut: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);
const DEMO_ACCESS_TOKEN = import.meta.env.VITE_DEMO_ACCESS_TOKEN?.trim();

export function AuthProvider({ children }: PropsWithChildren) {
  const [identity, setIdentity] = useState<ControlPlaneIdentity | null>(null);
  const [checking, setChecking] = useState(true);
  const [token, setToken] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    const existingToken = getAccessToken();
    const initialToken = existingToken || DEMO_ACCESS_TOKEN;

    if (!initialToken) {
      setChecking(false);
      return;
    }

    if (!existingToken && DEMO_ACCESS_TOKEN) {
      setAccessToken(DEMO_ACCESS_TOKEN);
    }

    api.auth.me()
      .then(setIdentity)
      .catch(() => clearAccessToken())
      .finally(() => setChecking(false));
  }, []);

  const signOut = useCallback(() => {
    clearAccessToken();
    setIdentity(null);
    setToken('');
    setError(null);
  }, []);

  const authenticate = async (accessToken: string) => {
    setSubmitting(true);
    setError(null);
    setAccessToken(accessToken);

    try {
      setIdentity(await api.auth.me());
      setToken('');
    } catch (caught) {
      clearAccessToken();
      setError(
        caught instanceof ApiError && caught.status === 401
          ? 'That access token is not valid.'
          : caught instanceof Error
            ? caught.message
            : 'Could not authenticate with the ReleaseGate API.',
      );
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const nextToken = token.trim();
    if (!nextToken) return;
    await authenticate(nextToken);
  };

  const value = useMemo<AuthContextValue | null>(() => {
    if (!identity) return null;

    return {
      identity,
      hasRole: (role: string) =>
        identity.roles.some((candidate) => candidate.toLowerCase() === role.toLowerCase()),
      signOut,
    };
  }, [identity, signOut]);

  if (checking) {
    return (
      <main className="access-screen">
        <div className="access-card">
          <span className="brand__mark">RG</span>
          <p className="eyebrow">ReleaseGate</p>
          <h1>Checking access…</h1>
        </div>
      </main>
    );
  }

  if (!value) {
    return (
      <main className="access-screen">
        <section className="access-card" aria-labelledby="access-title">
          <span className="brand__mark">RG</span>
          <p className="eyebrow">Release control plane</p>
          <h1 id="access-title">Access ReleaseGate</h1>
          <p>
            {DEMO_ACCESS_TOKEN
              ? 'Explore the portfolio deployment in read-only mode, or use a configured control-plane token.'
              : 'Enter a control-plane bearer token configured for this ReleaseGate deployment.'}
          </p>

          {DEMO_ACCESS_TOKEN && (
            <button
              className="button button--primary"
              type="button"
              disabled={submitting}
              onClick={() => void authenticate(DEMO_ACCESS_TOKEN)}
            >
              {submitting ? 'Opening demo…' : 'Open read-only demo'}
            </button>
          )}

          <form className="access-form" onSubmit={handleSubmit}>
            <div className="field">
              <label htmlFor="access-token">Access token</label>
              <input
                id="access-token"
                type="password"
                value={token}
                onChange={(event) => setToken(event.target.value)}
                placeholder="releasegate-…"
                autoComplete="off"
                autoFocus={!DEMO_ACCESS_TOKEN}
                required
              />
            </div>

            {error && <div className="form-error" role="alert">{error}</div>}

            <button className="button" type="submit" disabled={submitting}>
              {submitting ? 'Authenticating…' : 'Use access token'}
            </button>
          </form>
        </section>
      </main>
    );
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider.');
  }

  return context;
}
