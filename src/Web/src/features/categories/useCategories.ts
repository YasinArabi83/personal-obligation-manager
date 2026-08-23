import { useQuery } from '@tanstack/react-query';
import { apiFetch } from '../../shared/api/apiClient';
import type { Category } from '../../shared/types';

export function useCategories() {
  return useQuery({
    queryKey: ['categories'],
    queryFn: ({ signal }) => apiFetch<Category[]>('/categories', { signal }),
    staleTime: 5 * 60 * 1000,
  });
}
