import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../shared/api/error';
import { toEnglishDigits } from '../../shared/formatDate';
import { requestOtp, verifyOtp } from './auth.api';
import { useAuth } from './AuthContext';

const PHONE_PATTERN = /^09\d{9}$/;

export default function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [step, setStep] = useState<'phone' | 'code'>('phone');
  const [phoneInput, setPhoneInput] = useState('');
  const [codeInput, setCodeInput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const from =
    typeof location.state === 'object' &&
    location.state !== null &&
    'from' in location.state &&
    typeof location.state.from === 'string'
      ? location.state.from
      : '/';

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    const phoneNumber = toEnglishDigits(phoneInput).trim();

    if (step === 'phone') {
      if (!PHONE_PATTERN.test(phoneNumber)) {
        setError('شماره موبایل باید به شکل 09123456789 باشد.');
        return;
      }
      setSubmitting(true);
      try {
        await requestOtp(phoneNumber);
        setStep('code');
      } catch (cause) {
        setError(describe(cause));
      } finally {
        setSubmitting(false);
      }
      return;
    }

    const code = toEnglishDigits(codeInput).trim();
    if (code.length === 0) {
      setError('کد تایید را وارد کنید.');
      return;
    }
    setSubmitting(true);
    try {
      const result = await verifyOtp(phoneNumber, code);
      signIn(result.accessToken, result.refreshToken, result.user);
      navigate(from, { replace: true });
    } catch (cause) {
      setError(describe(cause));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-lg font-bold text-slate-900">ورود</h1>

        {step === 'phone' ? (
          <form onSubmit={handleSubmit} className="mt-4 space-y-4">
            <p className="text-sm text-slate-600">
              شماره موبایل خود را وارد کنید تا کد تایید پیامک شود.
            </p>
            <label className="block space-y-1">
              <span className="text-sm font-medium text-slate-700">
                شماره موبایل
              </span>
              <input
                type="tel"
                dir="ltr"
                autoComplete="tel"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-left text-slate-900 focus:border-sky-500 focus:outline-none"
                value={phoneInput}
                onChange={(event) => setPhoneInput(event.target.value)}
                placeholder="09123456789"
              />
            </label>
            <SubmitButton submitting={submitting} label="دریافت کد تایید" />
          </form>
        ) : (
          <form onSubmit={handleSubmit} className="mt-4 space-y-4">
            <p className="text-sm text-slate-600">
              کد تایید ارسال‌شده به شماره{' '}
              <span dir="ltr" className="font-medium">
                {toEnglishDigits(phoneInput).trim()}
              </span>{' '}
              را وارد کنید.
            </p>
            <label className="block space-y-1">
              <span className="text-sm font-medium text-slate-700">
                کد تایید
              </span>
              <input
                type="text"
                dir="ltr"
                inputMode="numeric"
                autoComplete="one-time-code"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-left tracking-widest text-slate-900 focus:border-sky-500 focus:outline-none"
                value={codeInput}
                onChange={(event) => setCodeInput(event.target.value)}
              />
            </label>
            <SubmitButton submitting={submitting} label="ورود" />
            <button
              type="button"
              className="text-sm text-sky-600 hover:underline"
              onClick={() => {
                setStep('phone');
                setCodeInput('');
                setError(null);
              }}
            >
              تغییر شماره
            </button>
          </form>
        )}

        {error && (
          <p role="alert" className="mt-4 text-sm text-red-600">
            {error}
          </p>
        )}
      </div>
    </main>
  );
}

function SubmitButton({
  submitting,
  label,
}: {
  submitting: boolean;
  label: string;
}) {
  return (
    <button
      type="submit"
      disabled={submitting}
      className="w-full rounded-md bg-sky-600 px-3 py-2 text-sm font-medium text-white hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-60"
    >
      {submitting ? 'در حال انجام…' : label}
    </button>
  );
}

function describe(cause: unknown): string {
  if (cause instanceof ApiError) {
    if (cause.retryAfterSeconds !== null) {
      return `${cause.message} (${cause.retryAfterSeconds} ثانیه دیگر تلاش کنید.)`;
    }
    return cause.message;
  }
  return 'ارتباط با سرور برقرار نشد.';
}
