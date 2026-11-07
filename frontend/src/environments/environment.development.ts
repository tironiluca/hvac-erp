// `ng serve` settings. Fill in your Supabase project here, or generate this file with
//   HVAC_SUPABASE_URL=... HVAC_SUPABASE_PUBLISHABLE_KEY=... node scripts/write-env.mjs --dev
// (the API defaults to the local backend). Keep real project values out of commits.
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5299/api',
  /** Sentry DSN for SPA error reporting; empty disables it (and keeps the SDK out of the bundle). */
  sentryDsn: '',
  /** UI language for users who have not picked one (`en` or `it`). */
  defaultLanguage: 'en',
  supabase: {
    url: 'https://your-project.supabase.co',
    publishableKey: 'your-supabase-publishable-key',
  },
};
