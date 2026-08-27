type DirectionalIconProps = {
  direction: 'left' | 'right' | 'arrow-right';
  className?: string;
};

export function DirectionalIcon({ direction, className }: DirectionalIconProps) {
  if (direction === 'left') {
    return (
      <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
        <path d="m12 5-5 5 5 5" />
      </svg>
    );
  }

  if (direction === 'arrow-right') {
    return (
      <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
        <path d="M4 10h11" />
        <path d="m11 6 4 4-4 4" />
      </svg>
    );
  }

  return (
    <svg className={className} viewBox="0 0 20 20" aria-hidden="true">
      <path d="m8 5 5 5-5 5" />
    </svg>
  );
}
