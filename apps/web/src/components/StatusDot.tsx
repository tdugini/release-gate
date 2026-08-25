type Props = {
  enabled: boolean;
};

export function StatusDot({ enabled }: Props) {
  return (
    <span className={`status-dot ${enabled ? 'status-dot--enabled' : ''}`}>
      <span aria-hidden="true" className="status-dot__mark" />
      {enabled ? 'Enabled' : 'Disabled'}
    </span>
  );
}
