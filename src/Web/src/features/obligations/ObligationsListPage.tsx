import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ApiError } from '../../shared/api/error';
import { useCategories } from '../categories/useCategories';
import { useAuth } from '../auth/AuthContext';
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
  const { signOut } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFilters(searchParams), [searchParams]);

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

  const {
    data,
    error,
    isError,
    isFetching,
    isPending,
    refetch,
  } = obligationsQuery;

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-4">
          <div>
            <h1 className="text-xl font-bold text-slate-900">
              مدیریت تعهدات شخصی
            </h1>
            <p className="text-xs text-slate-500">فهرست تعهدات</p>
          </div>
          <button
            type="button"
            onClick={signOut}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
          >
            خروج
          </button>
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
            <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
              <table className="min-w-full text-right">
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
    <div className="space-y-2 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
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
    <div className="rounded-xl border border-dashed border-slate-300 bg-white p-10 text-center">
      <p className="text-sm text-slate-500">تعهدی یافت نشد.</p>
      <p className="mt-1 text-xs text-slate-400">
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
      className="rounded-xl border border-red-200 bg-red-50 p-6 text-center"
    >
      <p className="text-sm text-red-700">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 rounded-md border border-red-300 bg-white px-3 py-1.5 text-sm text-red-700 hover:bg-red-100"
      >
        تلاش مجدد
      </button>
    </div>
  );
}
