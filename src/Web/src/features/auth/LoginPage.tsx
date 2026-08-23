import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { WarningCircle } from '@phosphor-icons/react';
import { ApiError } from '../../shared/api/error';
import { toEnglishDigits } from '../../shared/formatDate';
import { AppMark } from '../../shared/ui/AppMark';
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
    <main className="flex min-h-screen flex-col items-center justify-center bg-gradient-to-b from-teal-50 via-neutral-50 to-neutral-50 px-4">
      <div className="w-full max-w-sm">
        <div className="card p-8 sm:p-10">
          <div className="flex flex-col items-center gap-3 text-center">
            <AppMark size={48} />
            <div>
              <h1 className="text-lg font-bold text-slate-900">ورود</h1>
              <p className="mt-1 text-xs text-slate-500">مدیریت تعهدات شخصی</p>
            </div>
          </div>

          {step === 'phone' ? (
            <form onSubmit={handleSubmit} className="mt-8 space-y-5">
              <label className="block space-y-1.5">
                <span className="text-sm font-medium text-slate-700">
                  شماره موبایل
                </span>
                <input
                  type="tel"
                  dir="ltr"
                  autoComplete="tel"
                  className="field-input text-left"
                  value={phoneInput}
                  onChange={(event) => setPhoneInput(event.target.value)}
                  placeholder="09123456789"
                />
              </label>
              <SubmitButton submitting={submitting} label="دریافت کد تایید" />
              <p className="text-center text-xs leading-5 text-slate-400">
                کد تایید برای شماره واردشده پیامک می‌شود.
              </p>
            </form>
          ) : (
            <form onSubmit={handleSubmit} className="mt-8 space-y-5">
              <p className="text-sm leading-6 text-slate-600">
                کد تایید ارسال‌شده به شماره{' '}
                <span dir="ltr" className="font-medium tabular-nums text-slate-800">
                  {toEnglishDigits(phoneInput).trim()}
                </span>{' '}
                را وارد کنید.
              </p>
              <label className="block space-y-1.5">
                <span className="text-sm font-medium text-slate-700">
                  کد تایید
                </span>
                <input
                  type="text"
                  dir="ltr"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  className="field-input text-left tracking-[0.4em]"
                  value={codeInput}
                  onChange={(event) => setCodeInput(event.target.value)}
                  placeholder="––––––"
                />
              </label>
              <SubmitButton submitting={submitting} label="ورود" />
              <button
                type="button"
                className="btn-ghost mx-auto flex"
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
            <p
              role="alert"
              className="mt-5 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm leading-6 text-red-700"
            >
              <WarningCircle
                size={18}
                weight="fill"
                aria-hidden="true"
                className="mt-1 shrink-0"
              />
              <span>{error}</span>
            </p>
          )}
        </div>
        <p className="mt-4 text-center text-xs text-slate-400">
          ورود فقط با شماره موبایل و کد پیامکی انجام می‌شود.
        </p>
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
    <button type="submit" disabled={submitting} className="btn-primary w-full">
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
