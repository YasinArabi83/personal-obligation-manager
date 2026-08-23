import type { Obligation } from '../../shared/types';
import { formatDate } from '../../shared/formatDate';
import {
  PRIORITY_LABELS,
  PRIORITY_TEXT_CLASSES,
  STATUS_BADGE_CLASSES,
  STATUS_LABELS,
  TYPE_LABELS,
} from './labels';

interface ObligationRowProps {
  obligation: Obligation;
  categoryName: string;
}

export function ObligationRow({ obligation, categoryName }: ObligationRowProps) {
  const dueDate = formatDate(obligation.dueDate);
  const isOverdue = obligation.status === 'Overdue';

  return (
    <tr className="border-b border-slate-100 transition-colors duration-150 last:border-0 hover:bg-teal-50/40">
      <td className="px-4 py-3.5">
        <span className="block font-medium text-slate-900">
          {obligation.title}
        </span>
        {obligation.notes && (
          <span className="mt-0.5 block max-w-xs truncate text-xs text-slate-400">
            {obligation.notes}
          </span>
        )}
      </td>
      <td className="whitespace-nowrap px-4 py-3.5 text-sm text-slate-600">
        {TYPE_LABELS[obligation.type]}
      </td>
      <td className="whitespace-nowrap px-4 py-3.5 text-sm">
        <span className={PRIORITY_TEXT_CLASSES[obligation.priority]}>
          {PRIORITY_LABELS[obligation.priority]}
        </span>
      </td>
      <td
        className={`whitespace-nowrap px-4 py-3.5 text-sm tabular-nums ${
          dueDate === '—' ? 'text-slate-300' : isOverdue ? 'font-medium text-red-600' : 'text-slate-600'
        }`}
      >
        {dueDate}
      </td>
      <td className="whitespace-nowrap px-4 py-3.5 text-sm text-slate-600">
        {categoryName}
      </td>
      <td className="whitespace-nowrap px-4 py-3.5">
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset ${STATUS_BADGE_CLASSES[obligation.status]}`}
        >
          <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />
          {STATUS_LABELS[obligation.status]}
        </span>
      </td>
    </tr>
  );
}
