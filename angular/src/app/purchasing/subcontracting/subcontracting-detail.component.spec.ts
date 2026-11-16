import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SubcontractingDetailComponent } from './subcontracting-detail.component';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';

describe('SubcontractingDetailComponent Order Summary', () => {
  function createComponent() {
    const serviceMock = {
      getOrder: vi.fn().mockReturnValue(of({ id: 'sub-1', orderNumber: 'SCO-001', status: 1, items: [] })),
      getOrderSummary: vi.fn().mockReturnValue(of({
        id: 'sub-1',
        orderNumber: 'SCO-001',
        totalOrderedQty: 20,
        totalReceivedQty: 10,
        perReceived: 50,
      })),
      getReceiptsForOrder: vi.fn().mockReturnValue(of([])),
      getReceiptSummary: vi.fn(),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'sub-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: SubcontractingService, useValue: serviceMock },
        { provide: ConfirmationService, useValue: {} },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SubcontractingDetailComponent());
    comp.ngOnInit();
    return { comp, serviceMock };
  }

  it('loads order summary progress alongside order and receipts on init', () => {
    const { comp, serviceMock } = createComponent();
    expect(serviceMock.getOrderSummary).toHaveBeenCalledWith('sub-1');
    expect(comp.orderSummary()?.perReceived).toBe(50);
    expect(comp.orderSummary()?.totalReceivedQty).toBe(10);
    expect(comp.orderSummary()?.totalOrderedQty).toBe(20);
  });
});
