import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { StatusDot } from '../components/StatusDot';
import { useAsync } from '../hooks/useAsync';
import { api } from '../lib/api';

export function ProjectPage() {
  const { projectKey = '' } = useParams();
  const [environment, setEnvironment] = useState('production');

  const projectRequest = useAsync(() => api.projects.get(projectKey), [projectKey]);
  const flagsRequest = useAsync(
    () => api.flags.list(projectKey, environment),
    [projectKey, environment],
  );

  const environmentLabel = useMemo(
    () =>
      projectRequest.data?.environments.find((item) => item.key === environment)?.name ??
      environment,
    [environment, projectRequest.data],
  );

  if (projectRequest.loading) {
    return <div className="page"><div className="surface empty-state">Loading project…</div></div>;
  }

  if (projectRequest.error || !projectRequest.data) {
    return (
      <div className="page">
        <Link className="back-link" to="/">← Projects</Link>
        <div className="surface empty-state empty-state--error">
          Project could not be loaded.
        </div>
      </div>
    );
  }

  const project = projectRequest.data;

  return (
    <div className="page">
      <Link className="back-link" to="/">← Projects</Link>

      <header className="project-header">
        <div>
          <div className="project-header__title">
            <h1>{project.name}</h1>
            <code>{project.key}</code>
          </div>
          <p>{project.description ?? 'No project description.'}</p>
        </div>

        <div className="environment-switcher" role="group" aria-label="Environment">
          {project.environments.map((item) => (
            <button
              className={item.key === environment ? 'is-active' : ''}
              key={item.id}
              onClick={() => setEnvironment(item.key)}
            >
              {item.name}
            </button>
          ))}
        </div>
      </header>

      <section className="flags-header">
        <div>
          <p className="eyebrow">{environmentLabel}</p>
          <h2>Feature flags</h2>
        </div>
        <button className="button" disabled title="Coming in the next UI slice">
          Create flag
        </button>
      </section>

      <div className="flags-table">
        <div className="flags-table__head">
          <span>Flag</span>
          <span>Status</span>
          <span>Rollout</span>
          <span>Key</span>
          <span />
        </div>

        {flagsRequest.loading && <div className="flags-table__message">Loading flags…</div>}
        {flagsRequest.error && (
          <div className="flags-table__message flags-table__message--error">
            Could not load flags for this environment.
          </div>
        )}
        {!flagsRequest.loading && !flagsRequest.error && flagsRequest.data?.length === 0 && (
          <div className="flags-table__message">
            No flags in this project yet. Create one through the API to populate the control plane.
          </div>
        )}

        {flagsRequest.data?.map((flag) => (
          <Link
            className="flag-row"
            key={flag.id}
            to={`/projects/${project.key}/flags/${flag.key}`}
          >
            <div>
              <strong>{flag.name}</strong>
              <small>{flag.description ?? 'No description'}</small>
            </div>
            <StatusDot enabled={flag.enabled} />
            <span className="rollout">
              <span
                className="rollout__fill"
                style={{ width: `${flag.enabled ? flag.rolloutPercentage : 0}%` }}
              />
              <strong>{flag.enabled ? flag.rolloutPercentage : 0}%</strong>
            </span>
            <code>{flag.key}</code>
            <span className="row-arrow" aria-hidden="true">→</span>
          </Link>
        ))}
      </div>
    </div>
  );
}
