import { Link, useParams } from 'react-router-dom';
import { StatusDot } from '../components/StatusDot';
import { useAsync } from '../hooks/useAsync';
import { api } from '../lib/api';

export function FlagPage() {
  const { projectKey = '', flagKey = '' } = useParams();
  const { data: flag, loading, error } = useAsync(
    () => api.flags.get(projectKey, flagKey),
    [projectKey, flagKey],
  );

  return (
    <div className="page page--narrow">
      <Link className="back-link" to={`/projects/${projectKey}`}>
        ← Project
      </Link>

      {loading && <div className="surface empty-state">Loading flag…</div>}
      {error && <div className="surface empty-state empty-state--error">Flag could not be loaded.</div>}

      {flag && (
        <>
          <header className="flag-detail-header">
            <p className="eyebrow">Feature flag</p>
            <div>
              <h1>{flag.name}</h1>
              <code>{flag.key}</code>
            </div>
            <p>{flag.description ?? 'No description.'}</p>
          </header>

          <section className="environment-cards">
            {flag.environments.map((item) => (
              <article className="environment-card" key={item.environment}>
                <div className="environment-card__top">
                  <div>
                    <span>Environment</span>
                    <strong>{item.environment}</strong>
                  </div>
                  <StatusDot enabled={item.enabled} />
                </div>

                <div className="environment-card__rollout">
                  <div>
                    <span>Rollout</span>
                    <strong>{item.rolloutPercentage}%</strong>
                  </div>
                  <div className="progress">
                    <span style={{ width: `${item.rolloutPercentage}%` }} />
                  </div>
                </div>

                <footer>
                  Updated {new Date(item.updatedAt).toLocaleString()}
                </footer>
              </article>
            ))}
          </section>

          <aside className="decision-note">
            <span className="decision-note__label">Why environment state is separate</span>
            <p>
              The flag keeps one stable identity while rollout state changes independently
              across development, staging and production.
            </p>
          </aside>
        </>
      )}
    </div>
  );
}
