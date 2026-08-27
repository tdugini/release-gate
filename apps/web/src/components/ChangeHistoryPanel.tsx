import { useState } from 'react';
import { DirectionalIcon } from './DirectionalIcon';
import { useAuth } from './AuthProvider';
import { useToast } from './ToastProvider';
import { useAsync } from '../hooks/useAsync';
import { api } from '../lib/api';

const PAGE_SIZE = 10;

function formatTransitionState(
  enabled: boolean,
  rolloutPercentage: number,
  includeEnabledState: boolean,
) {
  if (!includeEnabledState) return `${rolloutPercentage}%`;
  return enabled ? `On · ${rolloutPercentage}%` : 'Off';
}

type ChangeHistoryPanelProps = {
  projectKey: string;
  flagKey: string;
  refreshKey?: number;
  onReviewed?: () => Promise<void> | void;
};

export function ChangeHistoryPanel({
  projectKey,
  flagKey,
  refreshKey = 0,
  onReviewed,
}: ChangeHistoryPanelProps) {
  const { identity, hasRole } = useAuth();
  const { showToast } = useToast();
  const canReview = hasRole('reviewer');
  const [page, setPage] = useState(1);
  const [reviewingChangeId, setReviewingChangeId] = useState<string | null>(null);

  const historyRequest = useAsync(
    () => api.flags.changeHistory(projectKey, flagKey, { page, pageSize: PAGE_SIZE }),
    [projectKey, flagKey, page, refreshKey],
  );

  const reviewChange = async (changeId: string, decision: 'approve' | 'reject') => {
    setReviewingChangeId(changeId);

    try {
      if (decision === 'approve') {
        await api.flags.approveChange(projectKey, flagKey, changeId);
      } else {
        await api.flags.rejectChange(projectKey, flagKey, changeId);
      }

      await historyRequest.reload();
      await onReviewed?.();
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

  const history = historyRequest.data;
  const firstItem = history && history.totalCount > 0
    ? (history.page - 1) * history.pageSize + 1
    : 0;
  const lastItem = history
    ? Math.min(history.page * history.pageSize, history.totalCount)
    : 0;

  return (
    <section className="change-history surface" aria-labelledby="change-history-title">
      <div className="change-history__header">
        <div>
          <p className="eyebrow">Audit log</p>
          <h2 id="change-history-title">Change history</h2>
        </div>
        <span>{history?.totalCount ?? 0} changes</span>
      </div>

      {historyRequest.loading && (
        <div className="change-history__state">Loading change history…</div>
      )}

      {historyRequest.error && (
        <div className="change-history__state change-history__state--error">
          Change history could not be loaded.
        </div>
      )}

      {history?.items.length === 0 && (
        <div className="change-history__state">
          No configuration changes have been recorded yet.
        </div>
      )}

      {history && history.items.length > 0 && (
        <>
          <div className="change-history__table-wrap">
            <table className="change-history__table">
              <thead>
                <tr>
                  <th scope="col">Environment</th>
                  <th scope="col">Change</th>
                  <th scope="col">Status</th>
                  <th scope="col">Requested</th>
                  <th scope="col">Reviewed</th>
                  <th scope="col" className="change-history__actions-heading">Review</th>
                </tr>
              </thead>
              <tbody>
                {history.items.map((change) => {
                  const isReviewing = reviewingChangeId === change.id;
                  const requestedByCurrentUser =
                    change.requestedBy.toLowerCase() === identity.subject.toLowerCase();
                  const canReviewChange = canReview && !requestedByCurrentUser;
                  const enabledStateChanged =
                    change.previousEnabled !== change.requestedEnabled;

                  return (
                    <tr className="change-history__row" key={change.id}>
                      <td>
                        <strong className="change-history__environment">
                          {change.environment}
                        </strong>
                      </td>
                      <td>
                        <div
                          className="change-history__transition"
                          aria-label={`Change from ${change.previousEnabled ? `enabled at ${change.previousRolloutPercentage}%` : 'disabled'} to ${change.requestedEnabled ? `enabled at ${change.requestedRolloutPercentage}%` : 'disabled'}`}
                        >
                          <span>
                            {formatTransitionState(
                              change.previousEnabled,
                              change.previousRolloutPercentage,
                              enabledStateChanged,
                            )}
                          </span>
                          <DirectionalIcon
                            direction="arrow-right"
                            className="change-history__transition-arrow"
                          />
                          <strong>
                            {formatTransitionState(
                              change.requestedEnabled,
                              change.requestedRolloutPercentage,
                              enabledStateChanged,
                            )}
                          </strong>
                        </div>
                      </td>
                      <td>
                        <span className={`change-status change-status--${change.status}`}>
                          {change.status}
                        </span>
                      </td>
                      <td>
                        <div className="change-history__person">
                          <strong>{change.requestedBy}</strong>
                          <time dateTime={change.requestedAt}>
                            {new Date(change.requestedAt).toLocaleString()}
                          </time>
                        </div>
                      </td>
                      <td>
                        {change.reviewedBy && change.reviewedAt ? (
                          <div className="change-history__person">
                            <strong>{change.reviewedBy}</strong>
                            <time dateTime={change.reviewedAt}>
                              {new Date(change.reviewedAt).toLocaleString()}
                            </time>
                          </div>
                        ) : (
                          <span className="change-history__empty-value">—</span>
                        )}
                      </td>
                      <td className="change-history__actions-cell">
                        {change.status === 'pending' && canReviewChange && (
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

                        {change.status === 'pending' && !canReviewChange && (
                          <span className="change-history__permission-note">
                            {requestedByCurrentUser
                              ? 'Awaiting reviewer'
                              : 'Reviewer required'}
                          </span>
                        )}

                        {change.status !== 'pending' && (
                          <span className="change-history__empty-value">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <footer className="change-history__pagination" aria-label="Change history pagination">
            <span>
              Showing {firstItem}–{lastItem} of {history.totalCount}
            </span>
            <div>
              <button
                className="pagination-button"
                type="button"
                disabled={history.page <= 1 || historyRequest.loading}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                aria-label="Previous page"
              >
                <DirectionalIcon direction="left" />
                Previous
              </button>
              <span className="pagination-page">
                Page {history.page} of {Math.max(history.totalPages, 1)}
              </span>
              <button
                className="pagination-button"
                type="button"
                disabled={history.totalPages === 0 || history.page >= history.totalPages || historyRequest.loading}
                onClick={() => setPage((current) => current + 1)}
                aria-label="Next page"
              >
                Next
                <DirectionalIcon direction="right" />
              </button>
            </div>
          </footer>
        </>
      )}
    </section>
  );
}
