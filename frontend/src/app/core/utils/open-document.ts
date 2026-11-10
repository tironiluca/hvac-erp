import { Observable } from 'rxjs';

/** A short-lived link to a private document (`GET …/pdf-url`, `GET …/attachments/{id}/url`). */
export interface DocumentUrl {
  url: string;
}

/**
 * Opens a private document in a new tab. Stored PDFs and photos have no permanent address: the API
 * issues a link that works for a few minutes, so the link is fetched at the moment of the click.
 *
 * The tab is opened first, inside the click, and pointed at the link when it arrives — a
 * `window.open` after the request has returned is what popup blockers stop.
 */
export function openDocument(
  link$: Observable<DocumentUrl>,
  onError: () => void,
  win: Window = window,
): void {
  const tab = win.open('', '_blank');
  link$.subscribe({
    next: ({ url }) => {
      if (tab) {
        tab.opener = null; // the document must not be able to reach back into the app
        tab.location.href = url;
      } else {
        win.location.href = url; // popups blocked outright: open it here instead
      }
    },
    error: () => {
      tab?.close();
      onError();
    },
  });
}
