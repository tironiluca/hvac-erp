import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { CurrentUser } from '../models/branding.model';
import { CurrentUserService } from './current-user.service';
import { TenantSelectionService } from './tenant-selection.service';

const ME = `${environment.apiUrl}/me`;

const member: CurrentUser = {
  email: 'anna@example.com',
  role: 'Admin',
  isPlatformAdmin: false,
  tenantId: 1,
  storeId: null,
  tenants: [
    {
      id: 1,
      name: 'Alfa',
      role: 'Admin',
      stores: [
        { id: 1, name: 'Centre', city: 'Bergamo' },
        { id: 2, name: 'North', city: null },
      ],
    },
  ],
};

const noAccess: CurrentUser = { ...member, role: null, tenantId: null, storeId: null };

describe('CurrentUserService', () => {
  let service: CurrentUserService;
  let selection: TenantSelectionService;
  let controller: HttpTestingController;

  /** Lets the service's promise chain reach its next request. */
  const settle = () => new Promise((resolve) => setTimeout(resolve));

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CurrentUserService);
    selection = TestBed.inject(TenantSelectionService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('exposes the tenant, its stores and the role once loaded', async () => {
    const loaded = service.ensureLoaded();
    controller.expectOne(ME).flush(member);
    await loaded;

    expect(service.hasAccess()).toBe(true);
    expect(service.isAdmin()).toBe(true);
    expect(service.isPlatformAdmin()).toBe(false);
    expect(service.currentTenant()?.name).toBe('Alfa');
    expect(service.stores().map((s) => s.name)).toEqual(['Centre', 'North']);
    expect(service.currentStore()).toBeNull(); // two stores, none selected
    expect(service.canSwitch()).toBe(true);
  });

  it('has nothing to switch between for one company with one store', async () => {
    const loaded = service.ensureLoaded();
    controller.expectOne(ME).flush({
      ...member,
      storeId: 1,
      tenants: [{ ...member.tenants[0], stores: [member.tenants[0].stores[0]] }],
    });
    await loaded;

    expect(service.canSwitch()).toBe(false);
    expect(service.currentStore()?.name).toBe('Centre');
  });

  it('reports no access for a signed-in user who belongs to no company', async () => {
    const loaded = service.ensureLoaded();
    controller.expectOne(ME).flush({ ...noAccess, tenants: [] });
    await loaded;

    expect(service.hasAccess()).toBe(false);
    expect(service.role()).toBeNull();
  });

  it('forgets a remembered tenant the user can no longer use and loads the default instead', async () => {
    selection.select(99, 5);

    const loaded = service.ensureLoaded();
    controller.expectOne(ME).flush(noAccess);
    await settle();
    controller.expectOne(ME).flush(member);
    await loaded;

    expect(selection.tenantId()).toBeNull();
    expect(selection.storeId()).toBeNull();
    expect(service.hasAccess()).toBe(true);
  });

  it('does not lock anyone out when the profile cannot be loaded', async () => {
    const loaded = service.ensureLoaded();
    controller.expectOne(ME).error(new ProgressEvent('error'));

    expect(await loaded).toBeNull();
    expect(service.hasAccess()).toBe(true);
  });

  it('forgets the profile and the selection on sign-out', async () => {
    selection.select(1, 2);
    const loaded = service.ensureLoaded();
    controller.expectOne(ME).flush({ ...member, storeId: 2 });
    await loaded;

    service.clear();

    expect(service.user()).toBeNull();
    expect(selection.tenantId()).toBeNull();
  });
});
