import {
  OBLIGATION_STATUSES,
  OBLIGATION_TYPES,
  type Category,
} from '../../shared/types';
import { STATUS_LABELS, TYPE_LABELS } from './labels';
import {
  hasActiveFilters,
  type ObligationFilters,
} from './filtersUrl';

interface ObligationsFiltersProps {
  filters: ObligationFilters;
  qInput: string;
  categories: Category[];
  onQInputChange: (value: string) => void;
  onPatch: (patch: Partial<ObligationFilters>) => void;
}

const SELECT_CLASS =
  'w-full rounded-md border border-slate-300 bg-white px-2 py-2 text-sm text-slate-700 focus:border-sky-500 focus:outline-none';

export function ObligationsFilters({
  filters,
  qInput,
  categories,
  onQInputChange,
  onPatch,
}: ObligationsFiltersProps) {
  return (
    <section
      aria-label="فیلترها"
      className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"
    >
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <label className="block space-y-1 sm:col-span-2 lg:col-span-1">
          <span className="text-xs font-medium text-slate-500">جستجو</span>
          <input
            type="search"
            value={qInput}
            onChange={(event) => onQInputChange(event.target.value)}
            placeholder="عنوان یا یادداشت…"
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-sky-500 focus:outline-none"
          />
        </label>

        <label className="block space-y-1">
          <span className="text-xs font-medium text-slate-500">وضعیت</span>
          <select
            value={filters.status}
            onChange={(event) => onPatch({ status: event.target.value as ObligationsFiltersProps['filters']['status'] })}
            className={SELECT_CLASS}
          >
            <option value="">همه</option>
            {OBLIGATION_STATUSES.map((status) => (
              <option key={status} value={status}>
                {STATUS_LABELS[status]}
              </option>
            ))}
          </select>
        </label>

        <label className="block space-y-1">
          <span className="text-xs font-medium text-slate-500">نوع</span>
          <select
            value={filters.type}
            onChange={(event) => onPatch({ type: event.target.value as ObligationsFiltersProps['filters']['type'] })}
            className={SELECT_CLASS}
          >
            <option value="">همه</option>
            {OBLIGATION_TYPES.map((type) => (
              <option key={type} value={type}>
                {TYPE_LABELS[type]}
              </option>
            ))}
          </select>
        </label>

        <label className="block space-y-1">
          <span className="text-xs font-medium text-slate-500">از تاریخ</span>
          <input
            type="date"
            value={filters.from}
            onChange={(event) => onPatch({ from: event.target.value })}
            className={SELECT_CLASS}
          />
        </label>

        <label className="block space-y-1">
          <span className="text-xs font-medium text-slate-500">تا تاریخ</span>
          <input
            type="date"
            value={filters.to}
            onChange={(event) => onPatch({ to: event.target.value })}
            className={SELECT_CLASS}
          />
        </label>

        <label className="block space-y-1">
          <span className="text-xs font-medium text-slate-500">دسته</span>
          <select
            value={filters.category}
            onChange={(event) => onPatch({ category: event.target.value })}
            className={SELECT_CLASS}
          >
            <option value="">همه</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      {hasActiveFilters(filters) && (
        <button
          type="button"
          onClick={() =>
            onPatch({ status: '', type: '', category: '', from: '', to: '', q: '' })
          }
          className="mt-3 text-xs text-sky-600 hover:underline"
        >
          پاک کردن فیلترها
        </button>
      )}
    </section>
  );
}
