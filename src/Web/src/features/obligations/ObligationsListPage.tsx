import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { SignOut, Tray, WarningCircle } from '@phosphor-icons/react';
import { ApiError } from '../../shared/api/error';
import { useCategories } from '../categories/useCategories';
import { useAuth } from '../auth/AuthContext';
import { AppMark } from '../../shared/ui/AppMark';
import { ObligationRow } from './ObligationRow';
import { ObligationsFilters } from './ObligationsFilters';
import { Pagination } from './Pagination';
import {
  parseFilters,
  toSearchParams,
  type ObligationFilters,
} from './filtersUrl';
import { useObligations } from './useObligations';

const SEARCH_DEBOUNCE_MS = 300;

export default function ObligationsListPage() {
  const { signOut, user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFilters(searchParams), [searchParams]);

  // Local input for the debounced q filter; re-synced whenever the URL changes externally.
  const [qInput, setQInput] = useState(filters.q);
  useEffect(() => {
    setQInput(filters.q);
  }, [filters.q]);

  const patchFilters = useCallback(
    (patch: Partial<ObligationFilters>) => {
      setSearchParams(
        (prev) => {
          const next = { ...parseFilters(prev), ...patch };
          if (!('page' in patch)) next.page = 1;
          return toSearchParams(next);
        },
        { replace: patch.q !== undefined },
      );
    },
    [setSearchParams],
  );

  useEffect(() => {
    if (qInput === filters.q) return;
    const timer = setTimeout(() => patchFilters({ q: qInput }), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [qInput, filters.q, patchFilters]);

  const obligationsQuery = useObligations(filters);
  const categoriesQuery = useCategories();
  const categories = useMemo(
    () => categoriesQuery.data ?? [],
    [categoriesQuery.data],
  );
  const categoryNameById = useMemo(
    () => new Map(categories.map((category) => [category.id, category.name])),
    [categories],
  );

  const { data, error, isError, isFetching, isPending, refetch } =
    obligationsQuery;

  return (
    <div className="min-h-screen bg-neutral-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-3 px-4 py-3">
          <div className="flex items-center gap-3">
            <AppMark size={36} />
            <div>
              <h1 className="text-base font-bold leading-6 text-slate-900">
                مدیریت تعهدات شخصی
              </h1>
              <p className="text-xs text-slate-400">فهرست تعهدات</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            {user && (
              <span
                dir="ltr"
                className="hidden rounded-full bg-slate-100 px-3 py-1 text-xs tabular-nums text-slate-500 sm:block"
              >
                {user.phoneNumber}
              </span>
            )}
            <button type="button" onClick={signOut} className="btn-secondary h-9 py-0">
              <SignOut size={14} aria-hidden="true" />
              خروج
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-5xl space-y-4 px-4 py-6">
        <ObligationsFilters
          filters={filters}
          qInput={qInput}
          categories={categories}
          onQInputChange={setQInput}
          onPatch={patchFilters}
        />

        {isPending ? (
          <LoadingSkeleton />
        ) : isError ? (
          <ErrorState
            message={
              error instanceof ApiError
                ? `${error.message} (${error.code})`
                : 'ارتباط با سرور برقرار نشد.'
            }
            onRetry={() => void refetch()}
          />
        ) : data.items.length === 0 ? (
          <EmptyState />
        ) : (
          <>
            <div className="card overflow-hidden">
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-right">
                  <thead className="bg-slate-50 text-xs text-slate-500">
                    <tr>
                      <th scope="col" className="px-4 py-3 font-medium">عنوان</th>
                      <th scope="col" className="px-4 py-3 font-medium">نوع</th>
                      <th scope="col" className="px-4 py-3 font-medium">اولویت</th>
                      <th scope="col" className="px-4 py-3 font-medium">سررسید</th>
                      <th scope="col" className="px-4 py-3 font-medium">دسته</th>
                      <th scope="col" className="px-4 py-3 font-medium">وضعیت</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {data.items.map((obligation) => (
                      <ObligationRow
                        key={obligation.id}
                        obligation={obligation}
                        categoryName={
                          obligation.categoryId
                            ? (categoryNameById.get(obligation.categoryId) ?? '—')
                            : '—'
                        }
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            {isFetching && (
              <p aria-live="polite" className="text-center text-xs text-slate-400">
                به‌روزرسانی…
              </p>
            )}

            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              totalCount={data.totalCount}
              disabled={isFetching}
              onPageChange={(page) => patchFilters({ page })}
            />
          </>
        )}
      </main>
    </div>
  );
}

function LoadingSkeleton() {
  return (
    <div
      role="status"
      aria-busy="true"
      className="card space-y-2 p-4"
    >
      <span className="sr-only">در حال بارگذاری…</span>
      {[0, 1, 2, 3, 4].map((row) => (
        <div
          key={row}
          className="h-10 animate-pulse rounded-md bg-slate-100"
        />
      ))}
    </div>
  );
}

function EmptyState() {
  return (
    <div className="card flex flex-col items-center gap-3 border-dashed p-12 text-center">
      <span
        aria-hidden="true"
        className="flex size-14 items-center justify-center rounded-full bg-teal-50 text-teal-600"
      >
        <Tray size={28} weight="duotone" />
      </span>
      <p className="text-sm font-medium text-slate-700">تعهدی یافت نشد.</p>
      <p className="text-xs leading-5 text-slate-400">
        فیلترها را تغییر دهید یا تعهد جدیدی بسازید.
      </p>
    </div>
  );
}

function ErrorState({
  message,
  onRetry,
}: {
  message: string;
  onRetry: () => void;
}) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center gap-3 rounded-xl border border-red-200 bg-red-50 p-8 text-center"
    >
      <WarningCircle size={28} weight="fill" aria-hidden="true" className="text-red-500" />
      <p className="text-sm text-red-700">{message}</p>
      <button type="button" onClick={onRetry} className="btn-primary h-9">
        تلاش مجدد
      </button>
    </div>
  );
}
