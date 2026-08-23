import { apiFetch } from '../../shared/api/apiClient';
import type { VerifyOtpResponse } from '../../shared/types';

export function requestOtp(phoneNumber: string): Promise<void> {
  return apiFetch<void>('/auth/otp/request', {
    method: 'POST',
    body: { phoneNumber },
    authenticated: false,
  });
}

export function verifyOtp(
  phoneNumber: string,
  code: string,
): Promise<VerifyOtpResponse> {
  return apiFetch<VerifyOtpResponse>('/auth/otp/verify', {
    method: 'POST',
    body: { phoneNumber, code },
    authenticated: false,
  });
}
