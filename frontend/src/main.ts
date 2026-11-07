import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app.component';
import { initErrorReporting } from './app/core/services/error-reporting';
import { migrateLegacyPrefs } from './app/core/utils/legacy-prefs';

migrateLegacyPrefs();
void initErrorReporting();

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
