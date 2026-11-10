import { of, Subject, throwError } from 'rxjs';
import { DocumentUrl, openDocument } from './open-document';

function fakeWindow(popupAllowed = true) {
  const tab = { opener: {} as unknown, location: { href: '' }, close: vi.fn() };
  const win = { open: vi.fn(() => (popupAllowed ? tab : null)), location: { href: 'app' } };
  return { tab, win: win as unknown as Window, raw: win };
}

describe('openDocument', () => {
  it('opens the tab during the click and points it at the link when it arrives', () => {
    const { tab, win, raw } = fakeWindow();
    const link$ = new Subject<DocumentUrl>();

    openDocument(link$, () => undefined, win);
    expect(raw.open).toHaveBeenCalledWith('', '_blank'); // before the link exists
    expect(tab.location.href).toBe('');

    link$.next({ url: 'https://storage.example/signed/q.pdf?token=t' });
    expect(tab.location.href).toBe('https://storage.example/signed/q.pdf?token=t');
    expect(tab.opener).toBeNull();
  });

  it('closes the empty tab and reports when no link can be issued', () => {
    const { tab, win } = fakeWindow();
    const onError = vi.fn();

    openDocument(
      throwError(() => new Error('404')),
      onError,
      win,
    );

    expect(tab.close).toHaveBeenCalled();
    expect(onError).toHaveBeenCalled();
  });

  it('falls back to the current tab when popups are blocked', () => {
    const { win, raw } = fakeWindow(false);

    openDocument(of({ url: 'https://storage.example/signed/q.pdf' }), () => undefined, win);

    expect(raw.location.href).toBe('https://storage.example/signed/q.pdf');
  });
});
