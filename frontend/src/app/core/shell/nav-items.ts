export interface NavItem {
  labelKey: string;
  icon: string;
  route?: string; // undefined until the module's phase ships
  adminOnly?: boolean; // hidden unless the signed-in user is an Admin
}

export const NAV_ITEMS: NavItem[] = [
  { labelKey: 'nav.dashboard', icon: 'dashboard', route: '/' },
  { labelKey: 'nav.clients', icon: 'people', route: '/clients' },
  { labelKey: 'nav.quotes', icon: 'description', route: '/quotes' },
  { labelKey: 'nav.repairs', icon: 'build', route: '/repairs' },
  { labelKey: 'nav.installations', icon: 'event', route: '/installations' },
  { labelKey: 'nav.fgas', icon: 'eco' },
  { labelKey: 'nav.settings', icon: 'palette', route: '/settings/branding', adminOnly: true },
  { labelKey: 'nav.pdfTemplates', icon: 'picture_as_pdf', route: '/settings/pdf-templates', adminOnly: true },
];
