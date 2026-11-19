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

  it('shows only held qty in unreserve dialog and warns when nothing left to unreserve (657ae2b763, 5e661bcf76)', () => {
    const { comp } = createComponent();
    const toasterInfo = vi.fn();
    (comp as any).toaster.info = toasterInfo;

    // Fully transferred/consumed reservation has 0 held qty -> shows info message instead of empty dialog
    comp.stockReservations.set([
      { id: 'sre-1', itemId: 'rm-1', reservedQty: 10, transferredQty: 6, consumedQty: 4, deliveredQty: 0, status: 1 },
    ]);
    expect(comp.hasHeldStockReservation).toBe(false);
    comp.openUnreserveModal();
    expect(toasterInfo).toHaveBeenCalledWith('No reserved stock to unreserve.');
    expect(comp.showUnreserveModal()).toBe(false);

    // Partial reservation has held qty = 10 - 3 - 2 = 5 -> opens dialog with held row only
    comp.stockReservations.set([
      { id: 'sre-1', itemId: 'rm-1', reservedQty: 10, transferredQty: 6, consumedQty: 4, deliveredQty: 0, status: 1 },
      { id: 'sre-2', itemId: 'rm-2', reservedQty: 10, transferredQty: 3, consumedQty: 2, deliveredQty: 0, status: 1 },
    ]);
    expect(comp.hasHeldStockReservation).toBe(true);
    expect(comp.heldStockReservations.length).toBe(1);
    expect(comp.getHeldQty(comp.heldStockReservations[0])).toBe(5);
    comp.openUnreserveModal();
    expect(comp.showUnreserveModal()).toBe(true);
  });
});
