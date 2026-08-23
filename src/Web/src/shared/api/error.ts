export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly retryAfterSeconds: number | null;

  constructor(
    status: number,
    code: string,
    message: string,
    retryAfterSeconds: number | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

function defaultCodeFor(status: number): string {
  switch (status) {
    case 400:
      return 'validation_error';
    case 401:
      return 'unauthorized';
    case 404:
      return 'not_found';
    case 429:
      return 'rate_limited';
    default:
      return status >= 500 ? 'server_error' : 'unknown';
  }
}

function defaultMessageFor(status: number): string {
  return status >= 500
    ? 'خطای سرور. بعداً تلاش کنید.'
    : 'درخواست با خطا مواجه شد.';
}

export async function toApiError(response: Response): Promise<ApiError> {
  const fallbackCode = defaultCodeFor(response.status);
  const retryAfterRaw = response.headers.get('retry-after');
  const retryAfterSeconds =
    retryAfterRaw !== null && /^\d+$/.test(retryAfterRaw)
      ? Number(retryAfterRaw)
      : null;

  let code = fallbackCode;
  let message = defaultMessageFor(response.status);
  try {
    const body: unknown = await response.json();
    if (
      body !== null &&
      typeof body === 'object' &&
      'error' in body &&
      body.error !== null &&
      typeof body.error === 'object'
    ) {
      const envelope = body.error as { code?: unknown; message?: unknown };
      if (typeof envelope.code === 'string') code = envelope.code;
      if (typeof envelope.message === 'string' && envelope.message.length > 0)
        message = envelope.message;
    }
  } catch {
    // Non-JSON error body — keep the defaults.
  }

  return new ApiError(response.status, code, message, retryAfterSeconds);
}
