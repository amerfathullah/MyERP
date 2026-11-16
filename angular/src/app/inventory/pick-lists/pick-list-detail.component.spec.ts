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
});
