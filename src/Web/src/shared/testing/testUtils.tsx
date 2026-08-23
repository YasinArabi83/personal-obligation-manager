import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReactElement } from 'react';
import { AuthProvider } from '../../features/auth/AuthProvider';

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

export function renderWithProviders(
  ui: ReactElement,
  { route = '/' }: { route?: string } = {},
) {
  const queryClient = createTestQueryClient();
  return {
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <AuthProvider>{ui}</AuthProvider>
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  };
}

export function jsonResponse(
  status: number,
  body: unknown,
  headers: Record<string, string> = {},
): Response {
  const headerEntries = Object.fromEntries(
    Object.entries(headers).map(([key, value]) => [key.toLowerCase(), value]),
  );
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: (name: string) => headerEntries[name.toLowerCase()] ?? null },
    json: async () => body,
  } as unknown as Response;
}

export function seedAuth(): void {
  window.localStorage.setItem('pom.auth.accessToken', 'access-token-1');
  window.localStorage.setItem('pom.auth.refreshToken', 'refresh-token-1');
  window.localStorage.setItem(
    'pom.auth.user',
    JSON.stringify({ id: 'u1', phoneNumber: '09123456789', displayName: null }),
  );
}

export type FetchMock = ReturnType<typeof vi.fn>;

export function stubFetch(impl: (input: RequestInfo | URL) => Response | Promise<Response>): FetchMock {
  const mock = vi.fn(impl);
  vi.stubGlobal('fetch', mock);
  return mock;
}

export function callsTo(mock: FetchMock, substring: string): { url: string; init?: RequestInit }[] {
  return mock.mock.calls
    .map((call) => ({
      url: String(call[0] ?? ''),
      init: call[1] as RequestInit | undefined,
    }))
    .filter((call) => call.url.includes(substring));
}
