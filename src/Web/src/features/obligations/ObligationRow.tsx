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
  return (
    <tr className="border-b border-slate-100 last:border-0 hover:bg-slate-50/60">
      <td className="px-4 py-3">
        <span className="block font-medium text-slate-900">
          {obligation.title}
        </span>
        {obligation.notes && (
          <span className="mt-0.5 block truncate text-xs text-slate-400">
            {obligation.notes}
          </span>
        )}
      </td>
      <td className="whitespace-nowrap px-4 py-3 text-sm text-slate-600">
        {TYPE_LABELS[obligation.type]}
      </td>
      <td className="whitespace-nowrap px-4 py-3 text-sm">
        <span className={PRIORITY_TEXT_CLASSES[obligation.priority]}>
          {PRIORITY_LABELS[obligation.priority]}
        </span>
      </td>
      <td
        className={`whitespace-nowrap px-4 py-3 text-sm ${formatDate(obligation.dueDate) === '—' ? 'text-slate-300' : 'text-slate-600'}`}
      >
        {formatDate(obligation.dueDate)}
      </td>
      <td className="whitespace-nowrap px-4 py-3 text-sm text-slate-600">
        {categoryName}
      </td>
      <td className="whitespace-nowrap px-4 py-3">
        <span
          className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${STATUS_BADGE_CLASSES[obligation.status]}`}
        >
          {STATUS_LABELS[obligation.status]}
        </span>
      </td>
    </tr>
  );
}
