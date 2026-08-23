import { CheckSquare } from '@phosphor-icons/react';

/** Product mark shared by auth and app pages. Decorative — the adjacent title carries meaning. */
export function AppMark({ size = 40 }: { size?: number }) {
  return (
    <span
      aria-hidden="true"
      className="inline-flex items-center justify-center rounded-xl bg-teal-700 text-white"
      style={{ width: size, height: size }}
    >
      <CheckSquare size={size * 0.6} weight="fill" />
    </span>
  );
}
