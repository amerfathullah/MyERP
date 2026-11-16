import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { SubcontractingInwardDetailComponent } from './subcontracting-inward-detail.component';
import { SubcontractingInwardOrderService } from '../../proxy/purchasing/subcontracting-inward-order.service';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';

describe('SubcontractingInwardDetailComponent Action Summary', () => {
  function createComponent() {
    const scioMock = {
      get: vi.fn().mockReturnValue(of({ id: 'scio-1', status: 1, items: [] })),
      getActionSummary: vi.fn().mockReturnValue(of({
        orderId: 'scio-1',
        pendingItemCount: 3,
        canClose: true,
        canReopen: false,
        canCancel: true,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'scio-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ConfirmationService, useValue: {} },
        { provide: SubcontractingInwardOrderService, useValue: scioMock },
        { provide: SubcontractingService, useValue: {} },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: SalesOrderService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
        { provide: SupplierService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
        { provide: ItemService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SubcontractingInwardDetailComponent());
    comp.ngOnInit();
    return { comp, scioMock };
  }

  it('loads actionSummary with pending item count and transition flags', () => {
    const { comp, scioMock } = createComponent();
    expect(scioMock.getActionSummary).toHaveBeenCalledWith('scio-1');
    expect(comp.actionSummary()?.pendingItemCount).toBe(3);
    expect(comp.actionSummary()?.canClose).toBe(true);
    expect(comp.actionSummary()?.canCancel).toBe(true);
  });
});
