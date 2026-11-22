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

  it('checks material transfer status per distinct RM itemId and removes only service item matching deleted purchaseOrderItemId (commits e9d264a6a4, 4211b7d593)', () => {
    const { comp } = createComponent();

    // RM-1 over-transferred (15/10), RM-2 short (0/5) -> Partial Material Transferred! (e9d264a6a4)
    expect(comp.getMaterialTransferStatus([
      { itemId: 'rm-1', requiredQty: 10, transferredQty: 15 },
      { itemId: 'rm-2', requiredQty: 5, transferredQty: 0 },
    ])).toBe('Partial Material Transferred');

    expect(comp.getMaterialTransferStatus([
      { itemId: 'rm-1', requiredQty: 10, transferredQty: 10 },
      { itemId: 'rm-2', requiredQty: 5, transferredQty: 5 },
    ])).toBe('Material Transferred');

    // Deleting row linked to po-item-1 keeps sibling row for same fg-1 on po-item-2 (4211b7d593)
    const remaining = comp.removePairedServiceItemByPurchaseOrderItem('po-item-1', 'fg-1', [
      { id: 'svc-1', purchaseOrderItemId: 'po-item-1', fgItemId: 'fg-1' },
      { id: 'svc-2', purchaseOrderItemId: 'po-item-2', fgItemId: 'fg-1' },
    ]);
    expect(remaining.length).toBe(1);
    expect(remaining[0].id).toBe('svc-2');
  });

  it('offers rejected-warehouse return only when hasRejectedQtyToReturn is true (PR #67404)', () => {
    const { comp } = createComponent();

    // Receipt with all rejected qty already returned -> toggle hidden and cannot enable
    comp.openReturnDialog({
      id: 'scr-1',
      receiptNumber: 'SCR-001',
      hasRejectedQtyToReturn: false,
      items: [{ itemId: 'fg-1', itemName: 'FG', qty: 8, rejectedQty: 2, rate: 50, warehouseId: 'wh-1', rejectedWarehouseId: 'rej-1' }],
    });
    expect(comp.canReturnFromRejectedWarehouse).toBe(false);
    comp.toggleReturnFromRejectedWarehouse(true);
    expect(comp.returnFromRejectedWarehouse()).toBe(false);
    expect(comp.returnItems()[0].returnQty).toBe(8);

    // Receipt with remaining rejected qty -> toggle shown and switches rows to rejected warehouse/qty
    comp.openReturnDialog({
      id: 'scr-2',
      receiptNumber: 'SCR-002',
      hasRejectedQtyToReturn: true,
      items: [{ itemId: 'fg-1', itemName: 'FG', qty: 8, rejectedQty: 2, rate: 50, warehouseId: 'wh-1', rejectedWarehouseId: 'rej-1' }],
    });
    expect(comp.canReturnFromRejectedWarehouse).toBe(true);
    comp.toggleReturnFromRejectedWarehouse(true);
    expect(comp.returnFromRejectedWarehouse()).toBe(true);
    expect(comp.returnItems()[0].returnQty).toBe(2);
    expect(comp.returnItems()[0].returnQtyFromRejectedWarehouse).toBe(true);
    expect(comp.returnItems()[0].warehouseId).toBe('rej-1');
  });
});
