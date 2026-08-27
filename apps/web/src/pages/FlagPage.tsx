import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ChangeHistoryPanel } from '../components/ChangeHistoryPanel';
import { Dialog } from '../components/Dialog';
import { DirectionalIcon } from '../components/DirectionalIcon';
import { useAuth } from '../components/AuthProvider';
import { StatusDot } from '../components/StatusDot';
import { useToast } from '../components/ToastProvider';
import { useAsync } from '../hooks/useAsync';
import { ApiError, api } from '../lib/api';

type EnvironmentDraft = {
  enabled: boolean;
  rolloutPercentage: number;
};

function getFieldError(error: unknown, field: string) {
  if (!(error instanceof ApiError)) return undefined;

  const match = Object.entries(error.fieldErrors).find(
    ([key]) => key.toLowerCase() === field.toLowerCase(),
  );

  return match?.[1]?.[0];
}

export function FlagPage() {
  const { projectKey = '', flagKey = '' } = useParams();
  const navigate = useNavigate();
  const { hasRole } = useAuth();
  const { showToast } = useToast();
  const canOperate = hasRole('operator');
  const flagRequest = useAsync(
    () => api.flags.get(projectKey, flagKey),
    [projectKey, flagKey],
  );
  const productionHistoryRequest = useAsync(
    () => api.flags.changeHistory(projectKey, flagKey, {
      page: 1,
      pageSize: 1,
      environment: 'production',
    }),
    [projectKey, flagKey],
  );
  const [drafts, setDrafts] = useState<Record<string, EnvironmentDraft>>({});
  const [savingEnvironment, setSavingEnvironment] = useState<string | null>(null);
  const [historyRefreshKey, setHistoryRefreshKey] = useState(0);
  const [manageOpen, setManageOpen] = useState(false);
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [manageError, setManageError] = useState<Error | null>(null);
  const [managing, setManaging] = useState(false);

  useEffect(() => {
    if (!flagRequest.data) return;

    setDrafts(
      Object.fromEntries(
        flagRequest.data.environments.map((item) => [
          item.environment,
          {
            enabled: item.enabled,
            rolloutPercentage: item.rolloutPercentage,
          },
        ]),
      ),
    );
  }, [flagRequest.data]);

  const pendingProductionChange = productionHistoryRequest.data?.items.find(
    (change) => change.status === 'pending',
  );

  const updateDraft = (
    environment: string,
    patch: Partial<EnvironmentDraft>,
  ) => {
    setDrafts((current) => ({
      ...current,
      [environment]: {
        ...current[environment],
        ...patch,
      },
    }));
  };

  const reloadFlagState = async () => {
    await Promise.all([flagRequest.reload(), productionHistoryRequest.reload()]);
  };

  const openManageDialog = () => {
    if (!flagRequest.data) return;
    setEditName(flagRequest.data.name);
    setEditDescription(flagRequest.data.description ?? '');
    setManageError(null);
    setManageOpen(true);
  };

  const closeManageDialog = () => {
    if (!managing) setManageOpen(false);
  };

  const openDeleteConfirmation = () => {
    setManageOpen(false);
    setDeleteConfirmOpen(true);
  };

  const closeDeleteConfirmation = () => {
    if (!managing) setDeleteConfirmOpen(false);
  };

  const saveFlag = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setManaging(true);
    setManageError(null);

    try {
      await api.flags.update(projectKey, flagKey, {
        name: editName.trim(),
        description: editDescription.trim() || undefined,
      });
      await flagRequest.reload();
      setManageOpen(false);
      showToast('Feature flag details updated.');
    } catch (caught) {
      const nextError = caught instanceof Error ? caught : new Error('Could not update feature flag.');
      setManageError(nextError);
      showToast(nextError.message, 'error');
    } finally {
      setManaging(false);
    }
  };

  const deleteFlag = async () => {
    if (!flagRequest.data) return;

    setManaging(true);
    setManageError(null);
    try {
      await api.flags.delete(projectKey, flagKey);
      showToast(`Feature flag ${flagRequest.data.name} deleted.`);
      navigate(`/projects/${projectKey}`);
    } catch (caught) {
      const nextError = caught instanceof Error ? caught : new Error('Could not delete feature flag.');
      setManageError(nextError);
      setDeleteConfirmOpen(false);
      setManageOpen(true);
      showToast(nextError.message, 'error');
      setManaging(false);
    }
  };

  const saveEnvironment = async (environment: string) => {
    const draft = drafts[environment];
    if (!draft) return;

    setSavingEnvironment(environment);

    try {
      await api.flags.updateEnvironment(projectKey, flagKey, environment, draft);
      await reloadFlagState();
      setHistoryRefreshKey((current) => current + 1);
      showToast(
        environment === 'production'
          ? 'Production change submitted for approval.'
          : `${environment} configuration saved.`,
      );
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : 'Could not save environment.';
      showToast(message, 'error');
    } finally {
      setSavingEnvironment(null);
    }
  };

  const editNameError = getFieldError(manageError, 'name');
  const editDescriptionError = getFieldError(manageError, 'description');

  return (
    <div className="page page--narrow">
      <Link
        className="back-link"
        to={`/projects/${projectKey}`}
        aria-label="Back to project"
      >
        <DirectionalIcon direction="left" />
      </Link>

      {flagRequest.loading && <div className="surface empty-state">Loading flag…</div>}
      {flagRequest.error && <div className="surface empty-state empty-state--error">Flag could not be loaded.</div>}

      {flagRequest.data && (
        <>
          <header className="flag-detail-header">
            <div className="flag-detail-header__titlebar">
              <div className="flag-detail-header__identity">
                <p className="eyebrow">Feature flag</p>
                <div className="flag-detail-header__name">
                  <h1>{flagRequest.data.name}</h1>
                  <code>{flagRequest.data.key}</code>
                </div>
                <p>{flagRequest.data.description ?? 'No description.'}</p>
              </div>
              {canOperate && (
                <button className="button entity-manage-button" type="button" onClick={openManageDialog}>
                  Manage flag
                </button>
              )}
            </div>
          </header>

          <section className="environment-cards">
            {flagRequest.data.environments.map((item) => {
              const draft = drafts[item.environment] ?? {
                enabled: item.enabled,
                rolloutPercentage: item.rolloutPercentage,
              };
              const isProduction = item.environment === 'production';
              const hasPendingChange = isProduction && Boolean(pendingProductionChange);
              const canEditEnvironment = canOperate && !hasPendingChange;
              const isSaving = savingEnvironment === item.environment;
              const isDirty =
                draft.enabled !== item.enabled ||
                draft.rolloutPercentage !== item.rolloutPercentage;
              const effectiveRollout = draft.enabled ? draft.rolloutPercentage : 0;

              return (
                <article className="environment-card environment-card--editable" key={item.environment}>
                  <div className="environment-card__top">
                    <div>
                      <span>Environment</span>
                      <strong>{item.environment}</strong>
                    </div>
                    <StatusDot enabled={draft.enabled} />
                  </div>

                  {isProduction && (
                    <div className={`production-warning${hasPendingChange ? ' production-warning--pending' : ''}`}>
                      {hasPendingChange
                        ? 'A production change is awaiting review. Approve or reject it in Change history before submitting another change.'
                        : 'Production changes require approval before they affect live traffic. Review both the enabled state and rollout percentage before submitting.'}
                    </div>
                  )}

                  <div className="environment-editor__controls">
                    <div className="toggle-field">
                      <span className="field__label">Status</span>
                      {canEditEnvironment ? (
                        <button
                          type="button"
                          className={`toggle-switch${draft.enabled ? ' is-enabled' : ''}`}
                          onClick={() => updateDraft(item.environment, { enabled: !draft.enabled })}
                          aria-pressed={draft.enabled}
                          aria-label={`${item.environment} flag status: ${draft.enabled ? 'enabled' : 'disabled'}`}
                        >
                          <span className="toggle-switch__track" aria-hidden="true">
                            <span className="toggle-switch__thumb" />
                          </span>
                          <span className="toggle-switch__label">
                            {draft.enabled ? 'Enabled' : 'Disabled'}
                          </span>
                        </button>
                      ) : (
                        <div className={`toggle-readonly${draft.enabled ? ' is-enabled' : ''}`}>
                          <span className="toggle-readonly__dot" aria-hidden="true" />
                          <strong>{draft.enabled ? 'Enabled' : 'Disabled'}</strong>
                        </div>
                      )}
                    </div>

                    <div className="field rollout-field">
                      <label htmlFor={`rollout-${item.environment}`}>Rollout</label>
                      {canEditEnvironment ? (
                        <div className="range-field">
                          <input
                            id={`rollout-${item.environment}`}
                            className="range-input"
                            type="range"
                            min="0"
                            max="100"
                            step="1"
                            value={draft.rolloutPercentage}
                            style={{
                              background: draft.enabled
                                ? `linear-gradient(to right, var(--accent) 0%, var(--accent) ${draft.rolloutPercentage}%, #dedde7 ${draft.rolloutPercentage}%, #dedde7 100%)`
                                : '#dedde7',
                            }}
                            onChange={(event) =>
                              updateDraft(item.environment, {
                                rolloutPercentage: Number(event.target.value),
                              })
                            }
                            disabled={!draft.enabled}
                          />
                          <span className="range-value">{effectiveRollout}%</span>
                        </div>
                      ) : (
                        <div className="range-readonly">
                          <strong>{effectiveRollout}%</strong>
                        </div>
                      )}
                      <small>
                        {!canOperate
                          ? 'Operator role required to change environment configuration.'
                          : hasPendingChange
                            ? 'The current production configuration remains active until the pending change is approved.'
                            : draft.enabled
                              ? 'Percentage of eligible traffic receiving the enabled variation.'
                              : 'Enable the flag to apply a rollout percentage.'}
                      </small>
                    </div>
                  </div>

                  <div className="environment-card__rollout">
                    <div>
                      <span>Effective rollout</span>
                      <strong>{effectiveRollout}%</strong>
                    </div>
                    <div className="progress" aria-hidden="true">
                      <span style={{ width: `${effectiveRollout}%` }} />
                    </div>
                  </div>

                  <footer className="environment-editor__footer">
                    <small>
                      {hasPendingChange
                        ? 'Production configuration unchanged while review is pending.'
                        : `Updated ${new Date(item.updatedAt).toLocaleString()}`}
                    </small>
                    {canOperate && hasPendingChange && (
                      <span className="pending-approval-status">
                        <span aria-hidden="true" />
                        Pending approval
                      </span>
                    )}
                    {canOperate && !hasPendingChange && !isDirty && (
                      <span className="saved-status">
                        <span aria-hidden="true">✓</span>
                        Saved
                      </span>
                    )}
                    {canOperate && !hasPendingChange && isDirty && (
                      <button
                        className="button button--primary"
                        type="button"
                        disabled={isSaving}
                        onClick={() => void saveEnvironment(item.environment)}
                      >
                        {isSaving
                          ? isProduction ? 'Submitting…' : 'Saving…'
                          : isProduction ? 'Submit for approval' : 'Save changes'}
                      </button>
                    )}
                  </footer>
                </article>
              );
            })}
          </section>

          <ChangeHistoryPanel
            projectKey={projectKey}
            flagKey={flagKey}
            refreshKey={historyRefreshKey}
            onReviewed={reloadFlagState}
          />

          <Dialog
            open={canOperate && manageOpen}
            title="Manage feature flag"
            description="Update flag metadata or permanently remove the flag and its environment configuration."
            onClose={closeManageDialog}
          >
            <form className="management-form" onSubmit={saveFlag}>
              <div className="field">
                <label htmlFor="flag-edit-name">Name</label>
                <input
                  id="flag-edit-name"
                  value={editName}
                  onChange={(event) => setEditName(event.target.value)}
                  autoFocus
                  required
                />
                {editNameError && <span className="field-error">{editNameError}</span>}
              </div>
              <div className="field">
                <label htmlFor="flag-edit-key">Key</label>
                <input id="flag-edit-key" value={flagRequest.data.key} disabled readOnly />
                <small>Keys are immutable because applications and SDK clients reference them.</small>
              </div>
              <div className="field">
                <label htmlFor="flag-edit-description">Description</label>
                <textarea
                  id="flag-edit-description"
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
                  <strong>Delete feature flag</strong>
                  <small>Deletes all environment settings and the flag audit history.</small>
                </div>
                <button
                  className="button button--danger"
                  type="button"
                  disabled={managing}
                  onClick={openDeleteConfirmation}
                >
                  Delete flag
                </button>
              </div>
              <div className="form-actions">
                <button className="button" type="button" onClick={closeManageDialog} disabled={managing}>
                  Cancel
                </button>
                <button className="button button--primary" type="submit" disabled={managing}>
                  {managing ? 'Saving…' : 'Save flag'}
                </button>
              </div>
            </form>
          </Dialog>

          <Dialog
            open={canOperate && deleteConfirmOpen}
            title="Delete feature flag?"
            description={`This permanently removes “${flagRequest.data.name}” and its audit history.`}
            onClose={closeDeleteConfirmation}
          >
            <div className="delete-confirmation">
              <div className="delete-confirmation__warning">
                <span className="delete-confirmation__icon" aria-hidden="true">!</span>
                <div>
                  <strong>This action cannot be undone.</strong>
                  <p>Applications referencing <code>{flagRequest.data.key}</code> will no longer receive this flag configuration.</p>
                </div>
              </div>
              <div className="form-actions delete-confirmation__actions">
                <button className="button" type="button" onClick={closeDeleteConfirmation} disabled={managing}>
                  Keep flag
                </button>
                <button className="button button--danger" type="button" onClick={() => void deleteFlag()} disabled={managing}>
                  {managing ? 'Deleting…' : 'Delete flag'}
                </button>
              </div>
            </div>
          </Dialog>
        </>
      )}
    </div>
  );
}
