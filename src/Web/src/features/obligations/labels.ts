import type { ObligationPriority, ObligationStatus, ObligationType } from '../../shared/types';

export const STATUS_LABELS: Record<ObligationStatus, string> = {
  Pending: 'در انتظار',
  Completed: 'انجام‌شده',
  Skipped: 'ردشده',
  Overdue: 'معوق',
  Archived: 'بایگانی',
};

export const TYPE_LABELS: Record<ObligationType, string> = {
  Task: 'کار',
  Payment: 'پرداخت',
  Document: 'سند',
  Subscription: 'اشتراک',
  Contract: 'قرارداد',
  Debt: 'بدهی',
  Maintenance: 'تعمیر و نگهداری',
  Appointment: 'قرار ملاقات',
  Custom: 'سفارشی',
};

export const PRIORITY_LABELS: Record<ObligationPriority, string> = {
  Low: 'کم',
  Medium: 'متوسط',
  High: 'زیاد',
};

/** Flat badge palette: tinted surface + readable text + hairline ring. */
export const STATUS_BADGE_CLASSES: Record<ObligationStatus, string> = {
  Pending: 'bg-sky-50 text-sky-700 ring-sky-600/25',
  Completed: 'bg-emerald-50 text-emerald-700 ring-emerald-600/25',
  Skipped: 'bg-zinc-100 text-zinc-600 ring-zinc-500/25',
  Overdue: 'bg-red-50 text-red-700 ring-red-600/25',
  Archived: 'bg-slate-100 text-slate-500 ring-slate-500/25',
};

export const PRIORITY_TEXT_CLASSES: Record<ObligationPriority, string> = {
  Low: 'text-slate-400',
  Medium: 'text-slate-600',
  High: 'font-medium text-red-600',
};
