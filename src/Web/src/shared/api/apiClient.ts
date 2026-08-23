import type { RefreshTokenResponse } from '../types';
import { ApiError, toApiError } from './error';
import * as tokens from './tokens';

const BASE_PATH = '/api/v1';
const SESSION_EXPIRED_MESSAGE = 'نشست شما منقضی شده است. دوباره وارد شوید.';

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  query?: Record<string, string | number | undefined | null>;
  /** Defaults to true; set false for auth endpoints that must never trigger a refresh. */
  authenticated?: boolean;
  signal?: AbortSignal;
}

let refreshInFlight: Promise<void> | null = null;
const sessionExpiredListeners = new Set<() => void>();

/** Registers a callback invoked when the session cannot be recovered (refresh failed). */
export function onSessionExpired(listener: () => void): () => void {
  sessionExpiredListeners.add(listener);
  return () => sessionExpiredListeners.delete(listener);
}

function buildUrl(path: string, query: RequestOptions['query']): string {
  const url = new URL(`${BASE_PATH}${path}`, window.location.origin);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') continue;
      url.searchParams.set(key, String(value));
    }
  }
  return `${url.pathname}${url.search}`;
}

async function refreshTokens(): Promise<void> {
  const refreshToken = tokens.getRefreshToken();
  if (!refreshToken) {
    throw new ApiError(401, 'unauthorized', SESSION_EXPIRED_MESSAGE);
  }

  const response = await fetch(`${BASE_PATH}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });
  if (!response.ok) throw await toApiError(response);

  const data = (await response.json()) as RefreshTokenResponse;
  tokens.updateTokens(data.accessToken, data.refreshToken);
}

/**
 * Single-flight refresh: parallel 401s share one /auth/refresh call.
 * Mandatory because the backend rotates refresh tokens and revokes the
 * whole token family when a consumed token is replayed (ADR-0017).
 */
function refreshOnce(): Promise<void> {
  refreshInFlight ??= refreshTokens().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

function notifySessionExpired(): void {
  tokens.clearAuth();
  for (const listener of sessionExpiredListeners) listener();
}

interface NormalizedRequest {
  url: string;
  init: RequestInit;
  authenticated: boolean;
}

function normalize(
  path: string,
  options: RequestOptions,
): NormalizedRequest | Promise<NormalizedRequest> {
  const { method = 'GET', body, query, authenticated = true, signal } = options;
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (!authenticated) {
    return {
      url: buildUrl(path, query),
      init: {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal,
      },
      authenticated,
    };
  }
  const accessToken = tokens.getAccessToken();
  const withAuth = (authHeader: string | null): NormalizedRequest => ({
    url: buildUrl(path, query),
    init: {
      method,
      headers: authHeader ? { ...headers, Authorization: authHeader } : headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    },
    authenticated,
  });
  return accessToken ? withAuth(`Bearer ${accessToken}`) : withAuth(null);
}

export async function apiFetch<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const first = await normalize(path, options);
  let response = await fetch(first.url, first.init);

  if (response.status === 401 && first.authenticated) {
    if (!tokens.getRefreshToken()) {
      notifySessionExpired();
      throw new ApiError(401, 'unauthorized', SESSION_EXPIRED_MESSAGE);
    }
    try {
      await refreshOnce();
    } catch {
      notifySessionExpired();
      throw new ApiError(401, 'unauthorized', SESSION_EXPIRED_MESSAGE);
    }
    // Retry exactly once with the fresh access token.
    const retry = await normalize(path, options);
    response = await fetch(retry.url, retry.init);
  }

  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}
