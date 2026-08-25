import { useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Dialog } from '../components/Dialog';
import { StatusDot } from '../components/StatusDot';
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

export function ProjectPage() {
  const { projectKey = '' } = useParams();
  const navigate = useNavigate();
  const { showToast } = useToast();
  const [environment, setEnvironment] = useState('production');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const [description, setDescription] = useState('');
  const [keyWasEdited, setKeyWasEdited] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<Error | null>(null);

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
      const flag = await api.flags.create(projectKey, {
        name: name.trim(),
        key: key.trim(),
        description: description.trim() || undefined,
      });

      setDialogOpen(false);
      showToast(`Flag ${flag.name} created.`);
      navigate(`/projects/${projectKey}/flags/${flag.key}`);
    } catch (caught) {
      const nextError = caught instanceof Error ? caught : new Error('Could not create flag.');
      setSubmitError(nextError);
      showToast(nextError.message, 'error');
    } finally {
      setSubmitting(false);
    }
  };

  const nameError = getFieldError(submitError, 'name');
  const keyError = getFieldError(submitError, 'key');

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
        <button className="button button--primary" type="button" onClick={openCreateDialog}>
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
            No flags in this project yet. Create one to start controlling releases.
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

      <Dialog
        open={dialogOpen}
        title="Create feature flag"
        description="The flag will be created across all project environments with rollout disabled by default."
        onClose={closeCreateDialog}
      >
        <form className="management-form" onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="flag-name">Name</label>
            <input
              id="flag-name"
              value={name}
              onChange={(event) => {
                const value = event.target.value;
                setName(value);
                if (!keyWasEdited) setKey(toSlug(value));
              }}
              placeholder="New checkout"
              autoFocus
              required
            />
            {nameError && <span className="field-error">{nameError}</span>}
          </div>

          <div className="field">
            <label htmlFor="flag-key">Key</label>
            <input
              id="flag-key"
              value={key}
              onChange={(event) => {
                setKey(toSlug(event.target.value));
                setKeyWasEdited(true);
              }}
              placeholder="new-checkout"
              required
            />
            <small>Stable identifier consumed by applications and API clients.</small>
            {keyError && <span className="field-error">{keyError}</span>}
          </div>

          <div className="field">
            <label htmlFor="flag-description">Description</label>
            <textarea
              id="flag-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="What behavior this flag controls."
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
              {submitting ? 'Creating…' : 'Create flag'}
            </button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
