import { visibleNavItems } from './nav-items';

const labels = (user: { isAdmin: boolean; isPlatformAdmin: boolean }) =>
  visibleNavItems(user).map((item) => item.labelKey);

describe('visibleNavItems', () => {
  it('hides tenant settings from non-admins and shared settings from everyone but platform administrators', () => {
    const sales = labels({ isAdmin: false, isPlatformAdmin: false });
    expect(sales).toContain('nav.quotes');
    expect(sales).not.toContain('nav.pdfTemplates');
    expect(sales).not.toContain('nav.settings');
  });

  it("shows a tenant's Admin their own settings but not the branding shared by every tenant", () => {
    const admin = labels({ isAdmin: true, isPlatformAdmin: false });
    expect(admin).toContain('nav.pdfTemplates');
    expect(admin).not.toContain('nav.settings');
  });

  it('shows a platform administrator the shared branding', () => {
    expect(labels({ isAdmin: true, isPlatformAdmin: true })).toContain('nav.settings');
  });
});
