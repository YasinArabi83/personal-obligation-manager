import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router-dom';
import userEvent from '@testing-library/user-event';
import { screen, waitFor } from '@testing-library/react';
import LoginPage from './LoginPage';
import {
  callsTo,
  jsonResponse,
  renderWithProviders,
  stubFetch,
} from '../../shared/testing/testUtils';

beforeEach(() => {
  window.localStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

function renderLogin() {
  renderWithProviders(
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<div>obligations-marker</div>} />
    </Routes>,
    { route: '/login' },
  );
}

async function submitPhone(
  user: ReturnType<typeof userEvent.setup>,
  phone: string,
) {
  await user.type(screen.getByLabelText(/شماره موبایل/), phone);
  await user.click(screen.getByRole('button', { name: 'دریافت کد تایید' }));
}

describe('LoginPage', () => {
  it('rejects an invalid phone number without calling the API', async () => {
    const mock = stubFetch(() => jsonResponse(204, undefined));
    const user = userEvent.setup();
    renderLogin();

    await submitPhone(user, '12345');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /شماره موبایل/,
    );
    expect(mock).not.toHaveBeenCalled();
  });

  it('normalizes Persian digits and moves to the code step after a successful OTP request', async () => {
    const mock = stubFetch((input) => {
      if (String(input).includes('/auth/otp/request')) {
        return jsonResponse(204, undefined);
      }
      throw new Error('unexpected call');
    });
    const user = userEvent.setup();
    renderLogin();

    await submitPhone(user, '۰۹۱۲۳۴۵۶۷۸۹');

    expect(
      await screen.findByText(/کد تایید ارسال‌شده به شماره/),
    ).toBeInTheDocument();
    const requestInit = callsTo(mock, '/auth/otp/request')[0].init;
    expect(JSON.parse(String(requestInit?.body))).toEqual({
      phoneNumber: '09123456789',
    });
  });

  it('surfaces the rate-limit message including Retry-After seconds', async () => {
    stubFetch(() =>
      jsonResponse(
        429,
        {
          error: {
            code: 'otp_rate_limited',
            message: 'Too many attempts. Try again later.',
          },
        },
        { 'Retry-After': '120' },
      ),
    );
    const user = userEvent.setup();
    renderLogin();

    await submitPhone(user, '09123456789');

    expect(await screen.findByRole('alert')).toHaveTextContent('120');
  });

  it('stores tokens and navigates to the list page after a successful verify', async () => {
    stubFetch((input) => {
      if (String(input).includes('/auth/otp/request')) {
        return jsonResponse(204, undefined);
      }
      return jsonResponse(200, {
        accessToken: 'access-1',
        expiresAt: '2026-01-01T00:00:00Z',
        refreshToken: 'refresh-1',
        user: { id: 'u1', phoneNumber: '09123456789', displayName: null },
      });
    });
    const user = userEvent.setup();
    renderLogin();

    await submitPhone(user, '09123456789');
    await user.type(await screen.findByLabelText(/کد تایید/), '123456');
    await user.click(screen.getByRole('button', { name: 'ورود' }));

    await waitFor(() =>
      expect(screen.getByText('obligations-marker')).toBeInTheDocument(),
    );
    expect(window.localStorage.getItem('pom.auth.accessToken')).toBe('access-1');
    expect(window.localStorage.getItem('pom.auth.refreshToken')).toBe(
      'refresh-1',
    );
  });

  it('shows the server error when the code is invalid', async () => {
    stubFetch((input) => {
      if (String(input).includes('/auth/otp/request')) {
        return jsonResponse(204, undefined);
      }
      return jsonResponse(401, {
        error: {
          code: 'otp_invalid_or_expired',
          message: 'The code is invalid or expired.',
        },
      });
    });
    const user = userEvent.setup();
    renderLogin();

    await submitPhone(user, '09123456789');
    await user.type(await screen.findByLabelText(/کد تایید/), '000000');
    await user.click(screen.getByRole('button', { name: 'ورود' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /code is invalid or expired/,
    );
    expect(window.localStorage.getItem('pom.auth.accessToken')).toBeNull();
  });
});
