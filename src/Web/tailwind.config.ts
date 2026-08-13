import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  // RTL-aware; Tailwind has no built-in logical-property flip, so the app
  // relies on dir="rtl" on <html> (index.html) + logical utilities where needed.
  theme: {
    extend: {},
  },
  plugins: [],
} satisfies Config;
