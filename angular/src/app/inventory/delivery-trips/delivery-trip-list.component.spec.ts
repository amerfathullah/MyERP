import { Injector, runInInjectionContext } from '@angular/core';
import { describe, expect, it, vi } from 'vitest';
import { LocalizationService, PermissionService } from '@abp/ng.core';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { DeliveryTripListComponent } from './delivery-trip-list.component';
import { DeliveryTripStore } from '../store/delivery-trip.store';

describe('DeliveryTripListComponent', () => {
  function createComponent(grantedPolicies: Record<string, boolean>) {
    const injector = Injector.create({
      providers: [
        { provide: DeliveryTripStore, useValue: { load: vi.fn(), notifyCustomers: vi.fn() } },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (policy: string) => grantedPolicies[policy] ?? false },
        },
      ],
    });
    return runInInjectionContext(injector, () => new DeliveryTripListComponent());
  }

  it('requires MyERP.DeliveryNotes permission when trip stops link Delivery Notes', () => {
    const tripWithDn = {
      id: 'trip-1',
      deliveryStops: [{ deliveryNoteId: 'dn-1', deliveryNoteNumber: 'DN-001' }],
    } as any;
    const tripWithoutDn = {
      id: 'trip-2',
      deliveryStops: [{ address: '123 Main St' }],
    } as any;

    const noDnRead = createComponent({ 'MyERP.DeliveryTrips.Edit': true, 'MyERP.DeliveryNotes': false });
    expect(noDnRead.canNotifyCustomers(tripWithDn)).toBe(false);
    expect(noDnRead.canNotifyCustomers(tripWithoutDn)).toBe(true);

    const withDnRead = createComponent({ 'MyERP.DeliveryTrips.Edit': true, 'MyERP.DeliveryNotes': true });
    expect(withDnRead.canNotifyCustomers(tripWithDn)).toBe(true);

    const noEdit = createComponent({ 'MyERP.DeliveryTrips.Edit': false, 'MyERP.DeliveryNotes': true });
    expect(noEdit.canNotifyCustomers(tripWithDn)).toBe(false);
  });
});
