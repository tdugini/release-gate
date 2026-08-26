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
      showToast(`${environment} configuration saved.`);
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : 'Could not save environment.';
      showToast(message, 'error');
    } finally {
      setSavingEnvironment(null);
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

                  {item.environment === 'production' && (
                    <div className="production-warning">
                      Production changes affect live traffic. Review both the enabled state and rollout percentage before saving.
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
                          disabled={!draft.enabled}
                        />
                        <span className="range-value">
                          {draft.enabled ? draft.rolloutPercentage : 0}%
                        </span>
                      </div>
                      <small>
                        {draft.enabled
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
                    <small>Updated {new Date(item.updatedAt).toLocaleString()}</small>
                    <button
                      className="button button--primary"
                      type="button"
                      disabled={!isDirty || isSaving}
                      onClick={() => void saveEnvironment(item.environment)}
                    >
                      {isSaving ? 'Saving…' : isDirty ? 'Save changes' : 'Saved'}
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
                {changesRequest.data.map((change) => (
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
                      <span>{change.requestedBy}</span>
                      <time dateTime={change.requestedAt}>
                        {new Date(change.requestedAt).toLocaleString()}
                      </time>
                    </footer>
                  </article>
                ))}
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
