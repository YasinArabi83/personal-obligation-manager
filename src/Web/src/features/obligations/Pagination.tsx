import { CaretLeft, CaretRight } from '@phosphor-icons/react';

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  disabled?: boolean;
  onPageChange: (page: number) => void;
}

export function Pagination({
  page,
  totalPages,
  totalCount,
  disabled = false,
  onPageChange,
}: PaginationProps) {
  if (totalPages <= 1) return null;

  return (
    <nav
      aria-label="صفحه‌بندی"
      className="card flex items-center justify-between px-4 py-3"
    >
      <span className="text-xs tabular-nums text-slate-500">
        مجموع {totalCount} تعهد — صفحه {page} از {totalPages}
      </span>
      <div className="flex gap-2">
        {/* RTL: «قبلی» points right, «بعدی» points left. */}
        <button
          type="button"
          disabled={disabled || page <= 1}
          onClick={() => onPageChange(page - 1)}
          className="btn-secondary h-9 py-0"
        >
          <CaretRight size={14} aria-hidden="true" weight="bold" />
          قبلی
        </button>
        <button
          type="button"
          disabled={disabled || page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          className="btn-secondary h-9 py-0"
        >
          بعدی
          <CaretLeft size={14} aria-hidden="true" weight="bold" />
        </button>
      </div>
    </nav>
  );
}
