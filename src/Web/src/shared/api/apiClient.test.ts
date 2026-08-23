import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './error';
import { apiFetch, onSessionExpired } from './apiClient';
import {
  callsTo,
  jsonResponse,
  seedAuth,
  stubFetch,
} from '../testing/testUtils';

beforeEach(() => {
  window.localStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

describe('apiFetch', () => {
  it('attaches the Bearer token from storage', async () => {
    seedAuth();
    const mock = stubFetch(() => jsonResponse(200, { ok: true }));

    await apiFetch('/obligations');

    const init = mock.mock.calls[0][1] as RequestInit;
    const headers = init.headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer access-token-1');
  });

  it('omits the Authorization header for unauthenticated requests', async () => {
    seedAuth();
    const mock = stubFetch(() => jsonResponse(204, undefined));

    await apiFetch('/auth/otp/request', { method: 'POST', authenticated: false });

    const init = mock.mock.calls[0][1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it('skips empty query params and encodes provided ones', async () => {
    const mock = stubFetch(() => jsonResponse(200, []));

    await apiFetch('/categories', {
      query: { status: 'Pending', q: '', page: 2, skip: undefined },
    });

    expect(mock.mock.calls[0][0]).toContain('/api/v1/categories?');
    expect(mock.mock.calls[0][0]).toContain('status=Pending');
    expect(mock.mock.calls[0][0]).toContain('page=2');
    expect(String(mock.mock.calls[0][0])).not.toContain('q=');
    expect(String(mock.mock.calls[0][0])).not.toContain('skip=');
  });

  it('refreshes once on 401 and retries the original request with the new token', async () => {
    seedAuth();
    let obligationsCalls = 0;
    const mock = stubFetch((input) => {
      if (String(input).includes('/auth/refresh')) {
        return jsonResponse(200, {
          accessToken: 'access-token-2',
          expiresAt: '2026-01-01T00:00:00Z',
          refreshToken: 'refresh-token-2',
        });
      }
      obligationsCalls += 1;
      return obligationsCalls === 1
        ? jsonResponse(401, { error: { code: 'unauthorized', message: 'expired' } })
        : jsonResponse(200, { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 });
    });

    const result = await apiFetch<{ items: unknown[]; totalCount: number }>('/obligations');

    expect(result.items).toEqual([]);
    expect(callsTo(mock, '/auth/refresh')).toHaveLength(1);
    expect(window.localStorage.getItem('pom.auth.accessToken')).toBe('access-token-2');
    expect(window.localStorage.getItem('pom.auth.refreshToken')).toBe('refresh-token-2');

    const retryInit = mock.mock.calls.at(-1)![1] as RequestInit;
    expect((retryInit.headers as Record<string, string>).Authorization).toBe(
      'Bearer access-token-2',
    );
  });

  it('shares a single refresh across concurrent 401s (single-flight)', async () => {
    seedAuth();
    let obligationsCalls = 0;
    const mock = stubFetch((input) => {
      if (String(input).includes('/auth/refresh')) {
        return jsonResponse(200, {
          accessToken: 'access-token-2',
          expiresAt: '2026-01-01T00:00:00Z',
          refreshToken: 'refresh-token-2',
        });
      }
      obligationsCalls += 1;
      return obligationsCalls <= 2
        ? jsonResponse(401, { error: { code: 'unauthorized', message: 'expired' } })
        : jsonResponse(200, []);
    });

    const [first, second] = await Promise.all([
      apiFetch('/obligations'),
      apiFetch('/categories'),
    ]);

    expect(first).toEqual([]);
    expect(second).toEqual([]);
    expect(callsTo(mock, '/auth/refresh')).toHaveLength(1);
  });

  it('clears auth and notifies listeners when refresh fails', async () => {
    seedAuth();
    const expiredListener = vi.fn();
    onSessionExpired(expiredListener);

    stubFetch(() =>
      jsonResponse(401, { error: { code: 'unauthorized', message: 'expired' } }),
    );

    await expect(apiFetch('/obligations')).rejects.toMatchObject({
      code: 'unauthorized',
    });

    expect(expiredListener).toHaveBeenCalledOnce();
    expect(window.localStorage.getItem('pom.auth.accessToken')).toBeNull();
    expect(window.localStorage.getItem('pom.auth.refreshToken')).toBeNull();
  });

  it('parses the standard error envelope into an ApiError', async () => {
    stubFetch(() =>
      jsonResponse(
        429,
        { error: { code: 'otp_rate_limited', message: 'بسیار زیاد' } },
        { 'Retry-After': '90' },
      ),
    );

    const error = (await apiFetch('/auth/otp/request', {
      method: 'POST',
      authenticated: false,
    }).catch((cause) => cause)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(429);
    expect(error.code).toBe('otp_rate_limited');
    expect(error.message).toBe('بسیار زیاد');
    expect(error.retryAfterSeconds).toBe(90);
  });
});
