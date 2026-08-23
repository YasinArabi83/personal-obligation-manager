/**
 * Placeholder Gregorian (YYYY-MM-DD) formatting for API dates.
 * Jalali rendering replaces this in one place when the shared Jalali
 * component lands (task 0011).
 */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

const PERSIAN_DIGITS = /[\u06F0-\u06F9\u0660-\u0669]/g;

/** Normalizes Persian/Arabic-Indic digits to ASCII so phone/code inputs work on all keyboards. */
export function toEnglishDigits(value: string): string {
  return value.replace(PERSIAN_DIGITS, (digit) =>
    String(digit.charCodeAt(0) & 0xf),
  );
}
