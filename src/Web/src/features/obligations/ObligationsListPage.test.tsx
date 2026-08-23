import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ObligationsListPage from './ObligationsListPage';
import {
  callsTo,
  jsonResponse,
  renderWithProviders,
  seedAuth,
  stubFetch,
} from '../../shared/testing/testUtils';
import type { Obligation } from '../../shared/types';

const CATEGORIES = [
  { id: 'cat-1', userId: null, name: 'Bills', isDefault: true, icon: null },
  { id: 'cat-2', userId: 'u1', name: 'خانه', isDefault: false, icon: null },
];

function obligation(overrides: Partial<Obligation> = {}): Obligation {
  return {
    id: 'o-1',
    type: 'Payment',
    title: 'قبض برق',
    notes: null,
    startDate: null,
    dueDate: '2026-08-30T12:00:00Z',
    endDate: null,
    status: 'Pending',
    priority: 'High',
    categoryId: 'cat-1',
    extraFields: '{}',
    isRecurring: false,
    createdAt: '2026-08-01T00:00:00Z',
    updatedAt: '2026-08-01T00:00:00Z',
    deletedAt: null,
    ...overrides,
  };
}

function listResponse(items: Obligation[], totalCount = items.length) {
  return jsonResponse(200, {
    items,
    totalCount,
    page: 1,
    pageSize: 20,
    totalPages: Math.ceil(totalCount / 20),
  });
}

beforeEach(() => {
  window.localStorage.clear();
  seedAuth();
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

describe('ObligationsListPage', () => {
  it('renders obligation rows with Persian labels and category names', async () => {
    stubFetch((input) =>
      String(input).includes('/categories')
        ? jsonResponse(200, CATEGORIES)
        : jsonResponse(200, {
            items: [
              obligation(),
              obligation({
                id: 'o-2',
                title: 'تمدید بیمه',
                type: 'Document',
                status: 'Completed',
                categoryId: null,
                dueDate: null,
              }),
            ],
            totalCount: 25,
            page: 1,
            pageSize: 20,
            totalPages: 2,
          }),
    );

    renderWithProviders(<ObligationsListPage />);

    const table = await screen.findByRole('table');
    expect(table).toHaveTextContent('قبض برق');
    expect(table).toHaveTextContent('تمدید بیمه');
    expect(table).toHaveTextContent('پرداخت');
    expect(table).toHaveTextContent('انجام‌شده');
    expect(table).toHaveTextContent('Bills');
    expect(screen.getByText(/مجموع 25 تعهد/)).toBeInTheDocument();
  });

  it('shows the empty state when the API returns no rows', async () => {
    stubFetch((input) =>
      String(input).includes('/categories')
        ? jsonResponse(200, [])
        : listResponse([]),
    );

    renderWithProviders(<ObligationsListPage />);

    expect(await screen.findByText('تعهدی یافت نشد.')).toBeInTheDocument();
  });

  it('sends documented query params when a status filter is chosen and resets to page 1', async () => {
    const mock = stubFetch((input) =>
      String(input).includes('/categories')
        ? jsonResponse(200, CATEGORIES)
        : listResponse([]),
    );
    const user = userEvent.setup();

    renderWithProviders(
      <>
        <ObligationsListPage />
      </>,
      { route: '/?page=3' },
    );

    await screen.findByRole('combobox', { name: /وضعیت/ });
    const initialCalls = callsTo(mock, '/obligations');
    expect(initialCalls[0].url).toContain('page=3');

    await user.selectOptions(
      screen.getByRole('combobox', { name: /وضعیت/ }),
      ['Completed'],
    );

    await waitFor(() => {
      const filtered = callsTo(mock, '/obligations').at(-1);
      expect(filtered?.url).toContain('status=Completed');
      expect(filtered?.url).not.toContain('page=3');
    });
  });

  it('debounces rapid typing into a single search request', async () => {
    const mock = stubFetch((input) =>
      String(input).includes('/categories')
        ? jsonResponse(200, [])
        : listResponse([]),
    );
    const user = userEvent.setup();

    renderWithProviders(<ObligationsListPage />);
    const search = await screen.findByRole('searchbox');

    await user.type(search, 'قبض');
    // Only the initial load call should exist before the debounce elapses.
    expect(callsTo(mock, '/obligations')).toHaveLength(1);

    await waitFor(
      () => expect(callsTo(mock, '/obligations')).toHaveLength(2),
      { timeout: 2000 },
    );
    const debounced = callsTo(mock, '/obligations')[1];
    expect(debounced.url).toContain('q=');
    expect(debounced.url).toContain(encodeURIComponent('قبض'));
  });

  it('requests page=2 when clicking next in pagination', async () => {
    const mock = stubFetch((input) => {
      if (String(input).includes('/categories')) return jsonResponse(200, []);
      return jsonResponse(200, {
        items: [obligation()],
        totalCount: 21,
        page: 1,
        pageSize: 20,
        totalPages: 2,
      });
    });
    const user = userEvent.setup();

    renderWithProviders(<ObligationsListPage />);

    await screen.findByRole('table');
    await user.click(screen.getByRole('button', { name: 'بعدی' }));

    await waitFor(() => {
      const last = callsTo(mock, '/obligations').at(-1);
      expect(last?.url).toContain('page=2');
    });
  });

  it('shows an error state with retry that refetches after recovery', async () => {
    let failing = true;
    stubFetch((input) => {
      if (String(input).includes('/categories')) return jsonResponse(200, []);
      if (failing) {
        return jsonResponse(500, {
          error: { code: 'server_error', message: 'خطای سرور' },
        });
      }
      return listResponse([obligation()]);
    });
    const user = userEvent.setup();

    renderWithProviders(<ObligationsListPage />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/خطای سرور/);
    expect(alert).toHaveTextContent('server_error');

    failing = false;
    await user.click(within(alert).getByRole('button', { name: 'تلاش مجدد' }));

    expect(await screen.findByRole('table')).toBeInTheDocument();
  });

  it('renders the loading skeleton on first load without a table', async () => {
    stubFetch(() => new Promise<Response>(() => {}));
    const { container } = renderWithProviders(<ObligationsListPage />);
    expect(await screen.findByLabelText(/فیلترها/)).toBeInTheDocument();
    expect(container.querySelector('.animate-pulse')).not.toBeNull();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});
