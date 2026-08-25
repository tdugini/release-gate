import { Link } from 'react-router-dom';
import { useAsync } from '../hooks/useAsync';
import { api } from '../lib/api';

export function ProjectsPage() {
  const { data: projects, loading, error } = useAsync(() => api.projects.list(), []);

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <p className="eyebrow">Control plane</p>
          <h1>Projects</h1>
          <p className="page-header__copy">
            Keep release controls close to the applications they belong to.
          </p>
        </div>
        <button className="button button--primary" disabled title="Included in the next UI slice">
          New project
        </button>
      </header>

      <section className="metric-strip" aria-label="Workspace summary">
        <div>
          <span>Projects</span>
          <strong>{projects?.length ?? '—'}</strong>
        </div>
        <div>
          <span>Policy</span>
          <strong>Environment scoped</strong>
        </div>
        <div>
          <span>Runtime</span>
          <strong>Self-hosted</strong>
        </div>
      </section>

      {loading && <div className="surface empty-state">Loading projects…</div>}
      {error && (
        <div className="surface empty-state empty-state--error">
          <strong>API unavailable</strong>
          <p>Start the ReleaseGate API on port 5080 to load project data.</p>
        </div>
      )}

      {!loading && !error && projects?.length === 0 && (
        <div className="surface empty-state">
          <span className="empty-state__index">01</span>
          <h2>No projects yet</h2>
          <p>Create one through the API to establish its development, staging and production environments.</p>
        </div>
      )}

      <div className="project-list">
        {projects?.map((project) => (
          <Link className="project-row" key={project.id} to={`/projects/${project.key}`}>
            <div>
              <strong>{project.name}</strong>
              <code>{project.key}</code>
            </div>
            <p>{project.description ?? 'No description'}</p>
            <div className="project-row__meta">
              <span>{project.flagCount} flags</span>
              <span>{project.environmentCount} envs</span>
              <span aria-hidden="true">→</span>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
