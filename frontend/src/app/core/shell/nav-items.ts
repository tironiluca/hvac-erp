export interface NavItem {
  labelKey: string;
  icon: string;
  route?: string; // undefined until the module's phase ships
  adminOnly?: boolean; // hidden unless the signed-in user is an Admin of the current tenant
  platformAdminOnly?: boolean; // hidden unless the signed-in user is a platform administrator
}

export const NAV_ITEMS: NavItem[] = [
  { labelKey: 'nav.dashboard', icon: 'dashboard', route: '/' },
  { labelKey: 'nav.clients', icon: 'people', route: '/clients' },
  { labelKey: 'nav.quotes', icon: 'description', route: '/quotes' },
  { labelKey: 'nav.repairs', icon: 'build', route: '/repairs' },
  { labelKey: 'nav.installations', icon: 'event', route: '/installations' },
  { labelKey: 'nav.fgas', icon: 'eco' },
  { labelKey: 'nav.settings', icon: 'palette', route: '/settings/branding', platformAdminOnly: true },
  { labelKey: 'nav.pdfTemplates', icon: 'picture_as_pdf', route: '/settings/pdf-templates', adminOnly: true },
];

/** The entries a user may open: one rule for the side navigation and the dashboard shortcuts. */
export function visibleNavItems(user: { isAdmin: boolean; isPlatformAdmin: boolean }): NavItem[] {
  return NAV_ITEMS.filter(
    (item) => (!item.adminOnly || user.isAdmin) && (!item.platformAdminOnly || user.isPlatformAdmin),
  );
}
