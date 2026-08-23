import {
  OBLIGATION_STATUSES,
  OBLIGATION_TYPES,
  type ObligationStatus,
  type ObligationType,
} from '../../shared/types';

export const DEFAULT_PAGE_SIZE = 20;
const MAX_PAGE_SIZE = 100;
const MAX_PAGE = 10_000;
const MAX_QUERY_LENGTH = 200;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

/** Filter state mirrored into URL query params (?status=&type=&category=&from=&to=&q=&page=&pageSize=). */
export interface ObligationFilters {
  status: ObligationStatus | '';
  type: ObligationType | '';
  category: string;
  from: string;
  to: string;
  q: string;
  page: number;
  pageSize: number;
}

export function emptyFilters(): ObligationFilters {
  return {
    status: '',
    type: '',
    category: '',
    from: '',
    to: '',
    q: '',
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  };
}

function trimmed(params: URLSearchParams, key: string): string {
  return (params.get(key) ?? '').trim();
}

function isOneOf<T extends string>(value: string, allowed: readonly T[]): value is T {
  return (allowed as readonly string[]).includes(value);
}

export function parseFilters(params: URLSearchParams): ObligationFilters {
  const filters = emptyFilters();

  const status = trimmed(params, 'status');
  if (isOneOf(status, OBLIGATION_STATUSES)) filters.status = status;

  const type = trimmed(params, 'type');
  if (isOneOf(type, OBLIGATION_TYPES)) filters.type = type;

  filters.category = trimmed(params, 'category');

  const from = trimmed(params, 'from');
  if (DATE_PATTERN.test(from)) filters.from = from;

  const to = trimmed(params, 'to');
  if (DATE_PATTERN.test(to)) filters.to = to;

  filters.q = trimmed(params, 'q').slice(0, MAX_QUERY_LENGTH);

  const page = Number.parseInt(trimmed(params, 'page'), 10);
  if (Number.isFinite(page)) filters.page = Math.min(Math.max(page, 1), MAX_PAGE);

  const pageSize = Number.parseInt(trimmed(params, 'pageSize'), 10);
  if (Number.isFinite(pageSize))
    filters.pageSize = Math.min(Math.max(pageSize, 1), MAX_PAGE_SIZE);

  return filters;
}

export function hasActiveFilters(filters: ObligationFilters): boolean {
  return (
    filters.status !== '' ||
    filters.type !== '' ||
    filters.category !== '' ||
    filters.from !== '' ||
    filters.to !== '' ||
    filters.q !== ''
  );
}

export function toSearchParams(filters: ObligationFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.status) params.set('status', filters.status);
  if (filters.type) params.set('type', filters.type);
  if (filters.category) params.set('category', filters.category);
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  if (filters.q) params.set('q', filters.q);
  if (filters.page > 1) params.set('page', String(filters.page));
  if (filters.pageSize !== DEFAULT_PAGE_SIZE)
    params.set('pageSize', String(filters.pageSize));
  return params;
}

/** Query record for GET /api/v1/obligations — omits unset filters, always sends pagination. */
export function buildApiQuery(
  filters: ObligationFilters,
): Record<string, string | number | undefined> {
  return {
    status: filters.status || undefined,
    type: filters.type || undefined,
    category: filters.category || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    q: filters.q || undefined,
    page: filters.page,
    pageSize: filters.pageSize,
  };
}
