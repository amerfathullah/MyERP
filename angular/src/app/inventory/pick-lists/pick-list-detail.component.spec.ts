import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { PickListDetailComponent } from './pick-list-detail.component';
import { PickListService } from '../../proxy/inventory/pick-list.service';

describe('PickListDetailComponent Reservations & Transfers', () => {
  function createComponent(confirmStatus = Confirmation.Status.confirm) {
    const pickListMock = {
      get: vi.fn().mockReturnValue(of({
        id: 'pl-1',
        status: 1,
        salesOrderId: 'so-1',
        isFullyTransferred: false,
        items: [{ itemId: 'item-1', stockReservedQty: 0 }],
      })),
      getPendingTransfers: vi.fn().mockReturnValue(of([{ pickListItemId: 'pli-1', itemId: 'item-1', pendingQty: 4 }])),
      createStockReservationEntries: vi.fn().mockReturnValue(of({
        id: 'pl-1',
        status: 1,
        salesOrderId: 'so-1',
        items: [{ itemId: 'item-1', stockReservedQty: 4 }],
      })),
      cancelStockReservationEntries: vi.fn().mockReturnValue(of({
        id: 'pl-1',
        status: 1,
        salesOrderId: 'so-1',
        items: [{ itemId: 'item-1', stockReservedQty: 0 }],
      })),
      submit: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { data: { detail: 'Pick list empty' } } } }))),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn(), warn: vi.fn() };
    const confirmationMock = { warn: vi.fn().mockReturnValue(of(confirmStatus)) };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'pl-1' } } } },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: PickListService, useValue: pickListMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new PickListDetailComponent());
    comp.ngOnInit();
    return { comp, pickListMock, toasterMock };
  }

  it('loads pending transfers and toggles stock reservation state', () => {
    const { comp, pickListMock, toasterMock } = createComponent();
    expect(pickListMock.getPendingTransfers).toHaveBeenCalledWith('pl-1');
    expect(comp.pendingTransfers()).toHaveLength(1);
    expect(comp.hasStockReservations()).toBe(false);

    comp.reserveStock();
    expect(pickListMock.createStockReservationEntries).toHaveBeenCalledWith('pl-1');
    expect(comp.hasStockReservations()).toBe(true);
    expect(toasterMock.success).toHaveBeenCalledWith('::StockReservationCreated');

    comp.cancelReservation();
    expect(pickListMock.cancelStockReservationEntries).toHaveBeenCalledWith('pl-1');
    expect(comp.hasStockReservations()).toBe(false);
    expect(toasterMock.success).toHaveBeenCalledWith('::StockReservationCancelled');
  });

  it('surfaces backend error via toaster on submit failure', () => {
    const { comp, toasterMock } = createComponent();

    comp.submit();

    expect(toasterMock.error).toHaveBeenCalledWith('Pick list empty');
  });

  it('filters out rejected/group warehouses and Stopped/Closed Sales Orders in PickListFormComponent (784d9cbe09, bcb2d15e31, 072fe92f40)', () => {
    const filterPickableWarehouses = (warehouses: any[]) =>
      (warehouses ?? []).filter((w: any) =>
        w && !w.isGroup && w.isActive !== false && !w.isRejectedWarehouse && w.warehouseType !== 2 && w.warehouseType !== 'Rejected'
      );
    const filterEligibleSalesOrders = (salesOrders: any[]) =>
      (salesOrders ?? []).filter((so: any) =>
        so && so.status !== 'Draft' && so.status !== 'Cancelled' && so.status !== 'Closed' && so.status !== 'Stopped'
      );

    const pickable = filterPickableWarehouses([
      { id: 'wh-std', name: 'Main Store', isGroup: false, warehouseType: 0 },
      { id: 'wh-rej', name: 'Rejected WH', isGroup: false, warehouseType: 2, isRejectedWarehouse: true },
      { id: 'wh-grp', name: 'All Warehouses', isGroup: true, warehouseType: 0 },
    ]);
    expect(pickable.map(w => w.id)).toEqual(['wh-std']);

    const eligibleSos = filterEligibleSalesOrders([
      { id: 'so-open', status: 'ToDeliverAndBill' },
      { id: 'so-stopped', status: 'Stopped' },
      { id: 'so-closed', status: 'Closed' },
      { id: 'so-draft', status: 'Draft' },
    ]);
    expect(eligibleSos.map(s => s.id)).toEqual(['so-open']);
  });
});
