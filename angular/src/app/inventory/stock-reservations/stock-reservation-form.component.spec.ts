import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { StockReservationFormComponent } from './stock-reservation-form.component';
import { StockReservationService } from '../../proxy/inventory/stock-reservation.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('StockReservationFormComponent Reserved Qty Lookup', () => {
  it('queries existing reserved qty when selecting an SO item with warehouse', () => {
    const reservationMock = {
      getReservedQty: vi.fn().mockReturnValue(of(14)),
    };
    const soMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'so-1', orderNumber: 'SO-1', status: 'ToDeliver' }] })),
      get: vi.fn().mockReturnValue(of({
        id: 'so-1',
        items: [{ id: 'soi-1', itemId: 'item-1', warehouseId: 'wh-1', quantity: 20, deliveredQty: 2 }],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: StockReservationService, useValue: reservationMock },
        { provide: SalesOrderService, useValue: soMock },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockReservationFormComponent());
    comp.ngOnInit();
    comp.form.patchValue({ salesOrderId: 'so-1' });
    comp.onSalesOrderChange();
    comp.form.patchValue({ soItemId: 'soi-1' });
    comp.onSoItemChange();

    expect(reservationMock.getReservedQty).toHaveBeenCalledWith('item-1', 'wh-1');
    expect(comp.existingReservedQty()).toBe(14);
    expect(comp.form.get('reservedQty')?.value).toBe(18);
  });
});
