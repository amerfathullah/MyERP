import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockReconciliationService } from '../../proxy/inventory';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import {
  computeReconciliationPreview,
  mapReconciliationPreviewRows,
  StockReconciliationFormComponent,
} from './stock-reconciliation-form.component';

describe('StockReconciliationFormComponent', () => {
  it('maps warehouse reconciliation previews with current quantity and valuation rate', () => {
    const rows = mapReconciliationPreviewRows([
      {
        itemId: 'item-1',
        itemCode: 'RM-01',
        itemName: 'Steel Bar',
        warehouseId: 'wh-1',
        currentQuantity: 25,
        currentValuationRate: 14.5,
      },
    ]);

    expect(rows).toHaveLength(1);
    expect(rows[0].currentQuantity).toBe(25);
    expect(rows[0].currentValuationRate).toBe(14.5);
    expect(rows[0].newQuantity).toBe(25);
    expect(rows[0].newValuationRate).toBe(14.5);
  });

  it('shows error toast and resets saving flag when create fails', () => {
    const toasterMock = { error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: StockReconciliationService, useValue: { create: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Period closed' } } }))) } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ItemService, useValue: {} },
        { provide: WarehouseService, useValue: {} },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockReconciliationFormComponent());
    comp.save();

    expect(comp.saving).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Period closed');
  });

  it('uses current qty or rate when new value is blank and refreshes to 0 when explicitly zeroed', () => {
    const blankPreview = computeReconciliationPreview({
      itemId: 'item-1',
      warehouseId: 'wh-1',
      currentQuantity: 10,
      currentValuationRate: 12.345,
      newQuantity: null,
      newValuationRate: null,
    });
    expect(blankPreview.effectiveQty).toBe(10);
    expect(blankPreview.effectiveRate).toBe(12.345);
    expect(blankPreview.currentAmount).toBe(123.45);
    expect(blankPreview.amountDifference).toBe(0);

    const zeroPreview = computeReconciliationPreview({
      itemId: 'item-1',
      warehouseId: 'wh-1',
      currentQuantity: 10,
      currentValuationRate: 12.345,
      newQuantity: 0,
      newValuationRate: 12.345,
    });
    expect(zeroPreview.newAmount).toBe(0);
    expect(zeroPreview.quantityDifference).toBe(-10);
    expect(zeroPreview.amountDifference).toBe(-123.45);
  });

  it('submits explicit zero newQuantity and falls back to currentQuantity when blank', () => {
    const createSpy = vi.fn().mockReturnValue(of({ id: 'sr-1' }));
    const injector = Injector.create({
      providers: [
        { provide: StockReconciliationService, useValue: { create: createSpy } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ItemService, useValue: {} },
        { provide: WarehouseService, useValue: {} },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockReconciliationFormComponent());
    comp.form.items = [
      { itemId: 'item-1', warehouseId: 'wh-1', currentQuantity: 5, currentValuationRate: 10, newQuantity: 0, newValuationRate: null },
      { itemId: 'item-2', warehouseId: 'wh-1', currentQuantity: 8, currentValuationRate: 15, newQuantity: null, newValuationRate: 20 },
    ];
    comp.save();

    expect(createSpy).toHaveBeenCalledTimes(1);
    const payload = createSpy.mock.calls[0][0];
    expect(payload.items[0].newQuantity).toBe(0);
    expect(payload.items[0].newValuationRate).toBe(10);
    expect(payload.items[1].newQuantity).toBe(8);
    expect(payload.items[1].newValuationRate).toBe(20);
  });

  it('keeps user-edited counted qty and rate when posting date changes while refreshing book values (PR #67442)', () => {
    const getBalanceSpy = vi.fn().mockImplementation((req: any) => {
      if (req.itemId === 'item-edited') {
        return of({ currentQuantity: 12, currentValuationRate: 18 });
      }
      return of({ currentQuantity: 20, currentValuationRate: 25 });
    });

    const injector = Injector.create({
      providers: [
        { provide: StockReconciliationService, useValue: { getStockBalanceFor: getBalanceSpy } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ItemService, useValue: {} },
        { provide: WarehouseService, useValue: {} },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockReconciliationFormComponent());
    comp.form.items = [
      // User edited both counted qty (7 vs book 10) and rate (15 vs book 14) -> both kept
      { itemId: 'item-edited', warehouseId: 'wh-1', currentQuantity: 10, currentValuationRate: 14, newQuantity: 7, newValuationRate: 15 },
      // User left qty & rate equal to book (5, 10) -> both follow new book values (20, 25)
      { itemId: 'item-untouched', warehouseId: 'wh-1', currentQuantity: 5, currentValuationRate: 10, newQuantity: 5, newValuationRate: 10 },
    ];

    comp.onPostingDateChanged('2026-11-20');

    expect(getBalanceSpy).toHaveBeenCalledTimes(2);
    expect(comp.form.items[0].currentQuantity).toBe(12);
    expect(comp.form.items[0].currentValuationRate).toBe(18);
    expect(comp.form.items[0].newQuantity).toBe(7);
    expect(comp.form.items[0].newValuationRate).toBe(15);

    expect(comp.form.items[1].currentQuantity).toBe(20);
    expect(comp.form.items[1].currentValuationRate).toBe(25);
    expect(comp.form.items[1].newQuantity).toBe(20);
    expect(comp.form.items[1].newValuationRate).toBe(25);
  });
});
