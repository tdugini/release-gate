import { useMemo, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Dialog } from '../components/Dialog';
import { DirectionalIcon } from '../components/DirectionalIcon';
import { useAuth } from '../components/AuthProvider';
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
  const { hasRole } = useAuth();
  const { showToast } = useToast();
  const canOperate = hasRole('operator');
  const [environment, setEnvironment] = useState('production');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const [description, setDescription] = useState('');
  const [keyWasEdited, setKeyWasEdited] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<Error | null>(null);
  const [updatingFlagKey, setUpdatingFlagKey] = useState<string | null>(null);
  const [submittedProductionFlags, setSubmittedProductionFlags] = useState<Set<string>>(
    () => new Set(),
  );
  const [manageOpen, setManageOpen] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [manageError, setManageError] = useState<Error | null>(null);
  const [managing, setManaging] = useState(false);

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

  const openManageDialog = () => {
    if (!projectRequest.data) return;
    setEditName(projectRequest.data.name);
    setEditDescription(projectRequest.data.description ?? '');
    setManageError(null);
    setManageOpen(true);
  };

  const closeManageDialog = () => {
    if (!managing) setManageOpen(false);
  };

  const selectEnvironment = (nextEnvironment: string) => {
    setEnvironment(nextEnvironment);
    setSubmittedProductionFlags(new Set());
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

  const saveProject = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setManaging(true);
    setManageError(null);

    try {
      await api.projects.update(projectKey, {
        name: editName.trim(),
        description: editDescription.trim() || undefined,
      });
      await projectRequest.reload();
      setManageOpen(false);
      showToast('Project details updated.');
    } catch (caught) {
      const nextError = caught instanceof Error ? caught : new Error('Could not update project.');
      setManageError(nextError);
      showToast(nextError.message, 'error');
    } finally {
      setManaging(false);
    }
  };

  const deleteProject = async () => {
    if (!projectRequest.data) return;
    if (!window.confirm(`Delete project "${projectRequest.data.name}" and all of its flags? This cannot be undone.`)) return;

    setManaging(true);
    setManageError(null);
    try {
      await api.projects.delete(projectKey);
      showToast(`Project ${projectRequest.data.name} deleted.`);
      navigate('/');
    } catch (caught) {
      const nextError = caught instanceof Error ? caught : new Error('Could not delete project.');
      setManageError(nextError);
      showToast(nextError.message, 'error');
      setManaging(false);
    }
  };

  const quickToggleFlag = async (
    flagKey: string,
    enabled: boolean,
    rolloutPercentage: number,
  ) => {
    const nextEnabled = !enabled;
    setUpdatingFlagKey(flagKey);

    try {
      await api.flags.updateEnvironment(projectKey, flagKey, environment, {
        enabled: nextEnabled,
        rolloutPercentage,
      });

      if (environment === 'production') {
        setSubmittedProductionFlags((current) => new Set(current).add(flagKey));
        showToast(
          `${nextEnabled ? 'Enable' : 'Disable'} request for ${flagKey} submitted for approval.`,
        );
      } else {
        await flagsRequest.reload();
        showToast(
          `${flagKey} ${nextEnabled ? 'enabled' : 'disabled'} in ${environment}.`,
        );
      }
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : 'Could not update flag.';
      showToast(message, 'error');
    } finally {
      setUpdatingFlagKey(null);
    }
  };

  const nameError = getFieldError(submitError, 'name');
  const keyError = getFieldError(submitError, 'key');
  const editNameError = getFieldError(manageError, 'name');
  const editDescriptionError = getFieldError(manageError, 'description');

  if (projectRequest.loading) {
    return <div className="page"><div className="surface empty-state">Loading project…</div></div>;
  }

  if (projectRequest.error || !projectRequest.data) {
    return (
      <div className="page">
        <Link className="back-link" to="/" aria-label="Back to projects">
          <DirectionalIcon direction="left" />
        </Link>
        <div className="surface empty-state empty-state--error">
          Project could not be loaded.
        </div>
      </div>
    );
  }

  const project = projectRequest.data;

  return (
    <div className="page">
      <Link className="back-link" to="/" aria-label="Back to projects">
        <DirectionalIcon direction="left" />
      </Link>

      <header className="project-header">
        <div>
          <div className="project-header__title">
            <h1>{project.name}</h1>
            <code>{project.key}</code>
          </div>
          <p>{project.description ?? 'No project description.'}</p>
        </div>

        <div className="project-header__controls">
          <div className="environment-switcher" role="group" aria-label="Environment">
            {project.environments.map((item) => (
              <button
                className={item.key === environment ? 'is-active' : ''}
                key={item.id}
                onClick={() => selectEnvironment(item.key)}
              >
                {item.name}
              </button>
            ))}
          </div>
          {canOperate && (
            <button className="button entity-manage-button" type="button" onClick={openManageDialog}>
              Manage project
            </button>
          )}
        </div>
      </header>

      <section className="flags-header">
        <div>
          <p className="eyebrow">{environmentLabel}</p>
          <h2>Feature flags</h2>
        </div>
        {canOperate && (
          <button className="button button--primary" type="button" onClick={openCreateDialog}>
            Create flag
          </button>
        )}
      </section>

      <div className="flags-table">
        <div className="flags-table__head">
          <span>Flag</span>
          <span>Status</span>
          <span>Rollout</span>
          <span>Key</span>
          <span>Action</span>
          <span aria-hidden="true" />
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

        {flagsRequest.data?.map((flag) => {
          const isUpdating = updatingFlagKey === flag.key;
          const productionRequestSubmitted = submittedProductionFlags.has(flag.key);

          return (
            <div className="flag-row" key={flag.id}>
              <Link
                className="flag-row__open"
                to={`/projects/${project.key}/flags/${flag.key}`}
                aria-label={`Open ${flag.name}`}
              />
              <div className="flag-row__identity">
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
              {canOperate ? (
                <button
                  className="flag-quick-action"
                  type="button"
                  disabled={isUpdating || productionRequestSubmitted}
                  onClick={() =>
                    void quickToggleFlag(flag.key, flag.enabled, flag.rolloutPercentage)
                  }
                >
                  {productionRequestSubmitted
                    ? 'Pending'
                    : isUpdating
                      ? 'Updating…'
                      : flag.enabled
                        ? 'Disable'
                        : 'Enable'}
                </button>
              ) : (
                <span />
              )}
              <DirectionalIcon direction="right" className="row-arrow" />
            </div>
          );
        })}
      </div>

      <Dialog
        open={canOperate && dialogOpen}
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

      <Dialog
        open={canOperate && manageOpen}
        title="Manage project"
        description="Update project metadata or permanently remove the project and all of its feature flags."
        onClose={closeManageDialog}
      >
        <form className="management-form" onSubmit={saveProject}>
          <div className="field">
            <label htmlFor="project-edit-name">Name</label>
            <input
              id="project-edit-name"
              value={editName}
              onChange={(event) => setEditName(event.target.value)}
              autoFocus
              required
            />
            {editNameError && <span className="field-error">{editNameError}</span>}
          </div>
          <div className="field">
            <label htmlFor="project-edit-key">Key</label>
            <input id="project-edit-key" value={project.key} disabled readOnly />
            <small>Keys are immutable because they are used in API and SDK references.</small>
          </div>
          <div className="field">
            <label htmlFor="project-edit-description">Description</label>
            <textarea
              id="project-edit-description"
              value={editDescription}
              onChange={(event) => setEditDescription(event.target.value)}
            />
            {editDescriptionError && <span className="field-error">{editDescriptionError}</span>}
          </div>
          {manageError && !(editNameError || editDescriptionError) && (
            <div className="form-error" role="alert">{manageError.message}</div>
          )}
          <div className="management-danger-zone">
            <div>
              <strong>Delete project</strong>
              <small>Deletes the project, environments, flags and audit history.</small>
            </div>
            <button className="button button--danger" type="button" disabled={managing} onClick={() => void deleteProject()}>
              Delete project
            </button>
          </div>
          <div className="form-actions">
            <button className="button" type="button" onClick={closeManageDialog} disabled={managing}>Cancel</button>
            <button className="button button--primary" type="submit" disabled={managing}>
              {managing ? 'Saving…' : 'Save project'}
            </button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
