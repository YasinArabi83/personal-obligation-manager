import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { apiFetch } from '../../shared/api/apiClient';
import type { ObligationListResponse } from '../../shared/types';
import { buildApiQuery, type ObligationFilters } from './filtersUrl';

export function useObligations(filters: ObligationFilters) {
  return useQuery({
    queryKey: ['obligations', filters],
    queryFn: ({ signal }) =>
      apiFetch<ObligationListResponse>('/obligations', {
        query: buildApiQuery(filters),
        signal,
      }),
    placeholderData: keepPreviousData,
  });
}
