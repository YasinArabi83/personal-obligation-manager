import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  // RTL-aware; Tailwind has no built-in logical-property flip, so the app
  // relies on dir="rtl" on <html> (index.html) + logical utilities (ps-/pe-/ms-/me-).
  theme: {
    extend: {
      fontFamily: {
        sans: ['Vazirmatn', 'ui-sans-serif', 'system-ui', 'Segoe UI', 'Tahoma', 'sans-serif'],
      },
      // POM design system (design-system/pom/MASTER.md):
      // Flat surfaces (borders over shadows), teal primary, orange accent reserved
      // for future primary CTAs. Interaction states defined once in index.css.
    },
  },
  plugins: [],
} satisfies Config;
