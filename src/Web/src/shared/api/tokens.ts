import type { AuthUser } from '../types';

const ACCESS_KEY = 'pom.auth.accessToken';
const REFRESH_KEY = 'pom.auth.refreshToken';
const USER_KEY = 'pom.auth.user';

function readString(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function getAccessToken(): string | null {
  return readString(ACCESS_KEY);
}

export function getRefreshToken(): string | null {
  return readString(REFRESH_KEY);
}

export function getUser(): AuthUser | null {
  const raw = readString(USER_KEY);
  if (!raw) return null;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'id' in parsed &&
      'phoneNumber' in parsed
    ) {
      return parsed as AuthUser;
    }
    return null;
  } catch {
    return null;
  }
}

export function setAuth(
  accessToken: string,
  refreshToken: string,
  user: AuthUser,
): void {
  window.localStorage.setItem(ACCESS_KEY, accessToken);
  window.localStorage.setItem(REFRESH_KEY, refreshToken);
  window.localStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function updateTokens(accessToken: string, refreshToken: string): void {
  window.localStorage.setItem(ACCESS_KEY, accessToken);
  window.localStorage.setItem(REFRESH_KEY, refreshToken);
}

export function clearAuth(): void {
  window.localStorage.removeItem(ACCESS_KEY);
  window.localStorage.removeItem(REFRESH_KEY);
  window.localStorage.removeItem(USER_KEY);
}
