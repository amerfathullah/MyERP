import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
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

  it('surfaces toaster error when submit, close, or cancel fails', () => {
    const scioMock = {
      get: vi.fn().mockReturnValue(of({ id: 'scio-1', status: 1, items: [] })),
      getActionSummary: vi.fn().mockReturnValue(of(null)),
      submit: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Submit failed' } } }))),
      close: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Close failed' } } }))),
      cancel: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Cancel failed' } } }))),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'scio-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: SubcontractingInwardOrderService, useValue: scioMock },
        { provide: SubcontractingService, useValue: {} },
        { provide: ToasterService, useValue: toasterMock },
        { provide: SalesOrderService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
        { provide: SupplierService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
        { provide: ItemService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SubcontractingInwardDetailComponent());
    comp.ngOnInit();

    comp.submit();
    expect(toasterMock.error).toHaveBeenCalledWith('Submit failed');

    comp.close();
    expect(toasterMock.error).toHaveBeenCalledWith('Close failed');

    comp.cancelOrder();
    expect(toasterMock.error).toHaveBeenCalledWith('Cancel failed');
  });

  it('allows opening receipt dialog for completed items when allowDeliveryOfOverproducedQty is enabled', () => {
    const scioMock = {
      get: vi.fn().mockReturnValue(of({
        id: 'scio-1',
        status: 3,
        items: [{ id: 'row-1', itemId: 'item-1', quantity: 10, receivedQty: 10, pendingReceiptQty: 0 }],
      })),
      getActionSummary: vi.fn().mockReturnValue(of({
        orderId: 'scio-1',
        pendingItemCount: 0,
        canClose: false,
        canReopen: false,
        canCancel: false,
        allowDeliveryOfOverproducedQty: true,
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
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new SubcontractingInwardDetailComponent());
    comp.ngOnInit();
    comp.openReceiptDialog();

    expect(comp.showReceiptDialog()).toBe(true);
    expect(comp.receiptItems()).toHaveLength(1);
    expect(comp.receiptItems()[0].itemId).toBe('item-1');
  });
});
