// Production settings. Deployments set them through HVAC_* environment variables, which
// scripts/write-env.mjs writes into this file at build time (see the README's Configuration
// section). The placeholders below are only what a build without those variables gets.
export const environment = {
  production: true,
  apiUrl: 'https://api.example.com/api',
  /** Sentry DSN for SPA error reporting; empty disables it (and keeps the SDK out of the bundle). */
  sentryDsn: '',
  /** UI language for users who have not picked one (`en` or `it`). */
  defaultLanguage: 'en',
  supabase: {
    url: 'https://your-project.supabase.co',
    publishableKey: 'your-supabase-publishable-key',
  },
};
