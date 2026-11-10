import { TestBed } from '@angular/core/testing';
import { TenantSelectionService } from './tenant-selection.service';

describe('TenantSelectionService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('starts with nothing selected', () => {
    const selection = TestBed.inject(TenantSelectionService);
    expect(selection.tenantId()).toBeNull();
    expect(selection.storeId()).toBeNull();
  });

  it('remembers the selection across reloads', () => {
    TestBed.inject(TenantSelectionService).select(3, 9);

    TestBed.resetTestingModule();
    const reloaded = TestBed.inject(TenantSelectionService);
    expect(reloaded.tenantId()).toBe(3);
    expect(reloaded.storeId()).toBe(9);
  });

  it('forgets a store when every store is selected, and everything on clear', () => {
    const selection = TestBed.inject(TenantSelectionService);
    selection.select(3, 9);
    selection.select(3, null);
    expect(localStorage.getItem('hvac-store')).toBeNull();
    expect(selection.tenantId()).toBe(3);

    selection.clear();
    expect(localStorage.getItem('hvac-tenant')).toBeNull();
    expect(selection.tenantId()).toBeNull();
  });

  it('ignores a corrupted stored value', () => {
    localStorage.setItem('hvac-tenant', 'not-a-number');
    localStorage.setItem('hvac-store', '-4');

    const selection = TestBed.inject(TenantSelectionService);
    expect(selection.tenantId()).toBeNull();
    expect(selection.storeId()).toBeNull();
  });
});
