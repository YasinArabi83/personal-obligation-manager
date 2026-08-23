import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import App from './App';
import {
  jsonResponse,
  renderWithProviders,
  seedAuth,
  stubFetch,
} from './shared/testing/testUtils';

beforeEach(() => {
  window.localStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

describe('App', () => {
  it('redirects unauthenticated users from / to the login page', () => {
    stubFetch(() => jsonResponse(200, []));
    renderWithProviders(<App />, { route: '/' });

    expect(screen.getByRole('heading', { level: 1, name: 'ورود' })).toBeInTheDocument();
  });

  it('renders the RTL obligations list heading when authenticated', async () => {
    seedAuth();
    stubFetch((input) =>
      String(input).includes('/categories')
        ? jsonResponse(200, [])
        : jsonResponse(200, { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }),
    );
    renderWithProviders(<App />, { route: '/' });

    expect(
      await screen.findByRole('heading', { level: 1, name: /مدیریت تعهدات شخصی/ }),
    ).toBeInTheDocument();
  });

  it('keeps unknown paths on the app instead of crashing', () => {
    stubFetch(() => jsonResponse(200, []));
    renderWithProviders(<App />, { route: '/nope' });

    expect(screen.getByRole('heading', { level: 1, name: 'ورود' })).toBeInTheDocument();
  });
});
