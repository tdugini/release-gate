import { useState } from 'react';
import { DirectionalIcon } from './DirectionalIcon';
import { useAuth } from './AuthProvider';
import { useToast } from './ToastProvider';
import { useAsync } from '../hooks/useAsync';
import { api } from '../lib/api';

const PAGE_SIZE = 10;

function formatFlagState(enabled: boolean, rolloutPercentage: number) {
  return enabled ? `Enabled · ${rolloutPercentage}%` : 'Disabled';
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
          <div className="change-history__list">
            {history.items.map((change) => {
              const isReviewing = reviewingChangeId === change.id;
              const requestedByCurrentUser =
                change.requestedBy.toLowerCase() === identity.subject.toLowerCase();
              const canReviewChange = canReview && !requestedByCurrentUser;

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
                    <DirectionalIcon
                      direction="arrow-right"
                      className="change-history__transition-arrow"
                    />
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
                    <div className="change-history__permission-note">
                      {requestedByCurrentUser
                        ? 'Another reviewer must review this production change.'
                        : 'Reviewer role required to approve or reject this production change.'}
                    </div>
                  )}
                </article>
              );
            })}
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
