import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { StatusDot } from '../components/StatusDot';
import { useToast } from '../components/ToastProvider';
import { useAsync } from '../hooks/useAsync';
import { api } from '../lib/api';

type EnvironmentDraft = {
  enabled: boolean;
  rolloutPercentage: number;
};

function formatFlagState(enabled: boolean, rolloutPercentage: number) {
  return enabled ? `Enabled · ${rolloutPercentage}%` : 'Disabled';
}

export function FlagPage() {
  const { projectKey = '', flagKey = '' } = useParams();
  const { showToast } = useToast();
  const flagRequest = useAsync(
    () => api.flags.get(projectKey, flagKey),
    [projectKey, flagKey],
  );
  const changesRequest = useAsync(
    () => api.flags.changes(projectKey, flagKey),
    [projectKey, flagKey],
  );
  const [drafts, setDrafts] = useState<Record<string, EnvironmentDraft>>({});
  const [savingEnvironment, setSavingEnvironment] = useState<string | null>(null);
  const [reviewingChangeId, setReviewingChangeId] = useState<string | null>(null);

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

  const pendingProductionChange = changesRequest.data?.find(
    (change) => change.environment === 'production' && change.status === 'pending',
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

  const saveEnvironment = async (environment: string) => {
    const draft = drafts[environment];
    if (!draft) return;

    setSavingEnvironment(environment);

    try {
      await api.flags.updateEnvironment(projectKey, flagKey, environment, draft);
      await Promise.all([flagRequest.reload(), changesRequest.reload()]);
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

  const reviewChange = async (changeId: string, decision: 'approve' | 'reject') => {
    setReviewingChangeId(changeId);

    try {
      if (decision === 'approve') {
        await api.flags.approveChange(projectKey, flagKey, changeId);
      } else {
        await api.flags.rejectChange(projectKey, flagKey, changeId);
      }

      await Promise.all([flagRequest.reload(), changesRequest.reload()]);
      showToast(
        decision === 'approve'
          ? 'Production change approved and applied.'
          : 'Production change rejected.',
      );
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : 'Could not review change.';
      showToast(message, 'error');
    } finally {
      setReviewingChangeId(null);
    }
  };

  return (
    <div className="page page--narrow">
      <Link className="back-link" to={`/projects/${projectKey}`}>
        ← Project
      </Link>

      {flagRequest.loading && <div className="surface empty-state">Loading flag…</div>}
      {flagRequest.error && <div className="surface empty-state empty-state--error">Flag could not be loaded.</div>}

      {flagRequest.data && (
        <>
          <header className="flag-detail-header">
            <p className="eyebrow">Feature flag</p>
            <div>
              <h1>{flagRequest.data.name}</h1>
              <code>{flagRequest.data.key}</code>
            </div>
            <p>{flagRequest.data.description ?? 'No description.'}</p>
          </header>

          <section className="environment-cards">
            {flagRequest.data.environments.map((item) => {
              const draft = drafts[item.environment] ?? {
                enabled: item.enabled,
                rolloutPercentage: item.rolloutPercentage,
              };
              const isProduction = item.environment === 'production';
              const hasPendingChange = isProduction && Boolean(pendingProductionChange);
              const isSaving = savingEnvironment === item.environment;
              const isDirty =
                draft.enabled !== item.enabled ||
                draft.rolloutPercentage !== item.rolloutPercentage;

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
                      <button
                        type="button"
                        className={draft.enabled ? 'is-enabled' : ''}
                        onClick={() => updateDraft(item.environment, { enabled: !draft.enabled })}
                        aria-pressed={draft.enabled}
                        disabled={hasPendingChange}
                      >
                        {draft.enabled ? 'Enabled' : 'Disabled'}
                      </button>
                    </div>

                    <div className="field">
                      <label htmlFor={`rollout-${item.environment}`}>Rollout</label>
                      <div className="range-field">
                        <input
                          id={`rollout-${item.environment}`}
                          className="range-input"
                          type="range"
                          min="0"
                          max="100"
                          step="1"
                          value={draft.rolloutPercentage}
                          onChange={(event) =>
                            updateDraft(item.environment, {
                              rolloutPercentage: Number(event.target.value),
                            })
                          }
                          disabled={!draft.enabled || hasPendingChange}
                        />
                        <span className="range-value">
                          {draft.enabled ? draft.rolloutPercentage : 0}%
                        </span>
                      </div>
                      <small>
                        {hasPendingChange
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
                      <strong>{draft.enabled ? draft.rolloutPercentage : 0}%</strong>
                    </div>
                    <div className="progress">
                      <span style={{ width: `${draft.enabled ? draft.rolloutPercentage : 0}%` }} />
                    </div>
                  </div>

                  <footer className="environment-editor__footer">
                    <small>
                      {hasPendingChange
                        ? 'Production configuration unchanged while review is pending.'
                        : `Updated ${new Date(item.updatedAt).toLocaleString()}`}
                    </small>
                    <button
                      className="button button--primary"
                      type="button"
                      disabled={!isDirty || isSaving || hasPendingChange}
                      onClick={() => void saveEnvironment(item.environment)}
                    >
                      {isSaving
                        ? isProduction ? 'Submitting…' : 'Saving…'
                        : hasPendingChange
                          ? 'Pending approval'
                          : isDirty
                            ? isProduction ? 'Submit for approval' : 'Save changes'
                            : 'Saved'}
                    </button>
                  </footer>
                </article>
              );
            })}
          </section>

          <section className="change-history surface" aria-labelledby="change-history-title">
            <div className="change-history__header">
              <div>
                <p className="eyebrow">Audit log</p>
                <h2 id="change-history-title">Change history</h2>
              </div>
              <span>{changesRequest.data?.length ?? 0} changes</span>
            </div>

            {changesRequest.loading && (
              <div className="change-history__state">Loading change history…</div>
            )}

            {changesRequest.error && (
              <div className="change-history__state change-history__state--error">
                Change history could not be loaded.
              </div>
            )}

            {changesRequest.data?.length === 0 && (
              <div className="change-history__state">
                No configuration changes have been recorded yet.
              </div>
            )}

            {changesRequest.data && changesRequest.data.length > 0 && (
              <div className="change-history__list">
                {changesRequest.data.map((change) => {
                  const isReviewing = reviewingChangeId === change.id;

                  return (
                    <article className="change-history__item" key={change.id}>
                      <div className="change-history__meta">
                        <strong>{change.environment}</strong>
                        <span className={`change-status change-status--${change.status}`}>
                          {change.status}
                        </span>
                      </div>

                      <div className="change-history__transition">
                        <span>
                          {formatFlagState(
                            change.previousEnabled,
                            change.previousRolloutPercentage,
                          )}
                        </span>
                        <span aria-hidden="true">→</span>
                        <strong>
                          {formatFlagState(
                            change.requestedEnabled,
                            change.requestedRolloutPercentage,
                          )}
                        </strong>
                      </div>

                      <footer>
                        <span>Requested by {change.requestedBy}</span>
                        <time dateTime={change.requestedAt}>
                          {new Date(change.requestedAt).toLocaleString()}
                        </time>
                      </footer>

                      {change.reviewedBy && change.reviewedAt && (
                        <div className="change-history__reviewed">
                          <span>Reviewed by {change.reviewedBy}</span>
                          <time dateTime={change.reviewedAt}>
                            {new Date(change.reviewedAt).toLocaleString()}
                          </time>
                        </div>
                      )}

                      {change.status === 'pending' && (
                        <div className="change-history__actions">
                          <button
                            className="button button--danger"
                            type="button"
                            disabled={isReviewing}
                            onClick={() => void reviewChange(change.id, 'reject')}
                          >
                            Reject
                          </button>
                          <button
                            className="button button--primary"
                            type="button"
                            disabled={isReviewing}
                            onClick={() => void reviewChange(change.id, 'approve')}
                          >
                            {isReviewing ? 'Reviewing…' : 'Approve'}
                          </button>
                        </div>
                      )}
                    </article>
                  );
                })}
              </div>
            )}
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
