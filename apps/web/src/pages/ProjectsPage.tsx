import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Dialog } from '../components/Dialog';
import { useToast } from '../components/ToastProvider';
import { useAsync } from '../hooks/useAsync';
import { ApiError, api } from '../lib/api';
import { toSlug } from '../lib/slug';

function getFieldError(error: unknown, field: string) {
  if (!(error instanceof ApiError)) return undefined;

  const match = Object.entries(error.fieldErrors).find(
    ([key]) => key.toLowerCase() === field.toLowerCase(),
  );

  return match?.[1]?.[0];
}

export function ProjectsPage() {
  const navigate = useNavigate();
  const { showToast } = useToast();
  const { data: projects, loading, error } = useAsync(() => api.projects.list(), []);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const [description, setDescription] = useState('');
  const [keyWasEdited, setKeyWasEdited] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<Error | null>(null);

  const openCreateDialog = () => {
    setName('');
    setKey('');
    setDescription('');
    setKeyWasEdited(false);
    setSubmitError(null);
    setDialogOpen(true);
  };

  const closeCreateDialog = () => {
    if (!submitting) setDialogOpen(false);
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setSubmitError(null);

    try {
      const project = await api.projects.create({
        name: name.trim(),
        key: key.trim(),
        description: description.trim() || undefined,
      });

      setDialogOpen(false);
      showToast(`Project ${project.name} created.`);
      navigate(`/projects/${project.key}`);
    } catch (caught) {
      const nextError = caught instanceof Error ? caught : new Error('Could not create project.');
      setSubmitError(nextError);
      showToast(nextError.message, 'error');
    } finally {
      setSubmitting(false);
    }
  };

  const nameError = getFieldError(submitError, 'name');
  const keyError = getFieldError(submitError, 'key');

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
        <button className="button button--primary" type="button" onClick={openCreateDialog}>
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
          <p>Create your first project to establish its development, staging and production environments.</p>
          <button className="button button--primary" type="button" onClick={openCreateDialog}>
            Create project
          </button>
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

      <Dialog
        open={dialogOpen}
        title="Create project"
        description="Projects automatically receive development, staging and production environments."
        onClose={closeCreateDialog}
      >
        <form className="management-form" onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="project-name">Name</label>
            <input
              id="project-name"
              value={name}
              onChange={(event) => {
                const value = event.target.value;
                setName(value);
                if (!keyWasEdited) setKey(toSlug(value));
              }}
              placeholder="Checkout service"
              autoFocus
              required
            />
            {nameError && <span className="field-error">{nameError}</span>}
          </div>

          <div className="field">
            <label htmlFor="project-key">Key</label>
            <input
              id="project-key"
              value={key}
              onChange={(event) => {
                setKey(toSlug(event.target.value));
                setKeyWasEdited(true);
              }}
              placeholder="checkout-service"
              required
            />
            <small>Stable identifier used in API paths. Lowercase letters, numbers and hyphens only.</small>
            {keyError && <span className="field-error">{keyError}</span>}
          </div>

          <div className="field">
            <label htmlFor="project-description">Description</label>
            <textarea
              id="project-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="What this project controls and who owns it."
            />
          </div>

          {submitError && !(nameError || keyError) && (
            <div className="form-error" role="alert">{submitError.message}</div>
          )}

          <div className="form-actions">
            <button className="button" type="button" onClick={closeCreateDialog} disabled={submitting}>
              Cancel
            </button>
            <button className="button button--primary" type="submit" disabled={submitting}>
              {submitting ? 'Creating…' : 'Create project'}
            </button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
