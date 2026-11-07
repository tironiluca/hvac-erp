import { TestBed } from '@angular/core/testing';
import { TranslocoService, TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  function setup(defaultLang: string): LanguageService {
    TestBed.configureTestingModule({
      imports: [TranslocoTestingModule.forRoot({ langs: { en: {}, it: {} }, translocoConfig: { availableLangs: ['en', 'it'], defaultLang } })],
    });
    return TestBed.inject(LanguageService);
  }

  beforeEach(() => localStorage.clear());

  it("starts in the deployment's default language when the user has not picked one", () => {
    const service = setup('it');

    expect(service.lang()).toBe('it');
    expect(document.documentElement.lang).toBe('it');
  });

  it('prefers the stored choice and persists a switch', () => {
    localStorage.setItem('hvac-lang', 'it');
    const service = setup('en');
    expect(service.lang()).toBe('it');

    service.set('en');

    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(localStorage.getItem('hvac-lang')).toBe('en');
  });

  it('ignores an unknown language', () => {
    const service = setup('en');

    service.set('fr');

    expect(service.lang()).toBe('en');
  });
});
