import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CompanyService } from '../../../proxy/core/company.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { StockBalanceService } from '../../../proxy/inventory/stock-balance.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { QuickStockBalanceComponent } from './quick-stock-balance.component';

describe('QuickStockBalanceComponent', () => {
  it('resolves item and balance from barcode input', () => {
    const stockBalanceMock = {
      getQuickStockBalance: vi.fn().mockReturnValue(
        of({
          itemId: 'item-1',
          itemCode: 'SKU-1',
          itemName: 'Widget',
          uom: 'Unit',
          warehouseId: 'wh-1',
          warehouseName: 'Main WH',
          asOfDate: '2026-06-15',
          qty: 12,
          value: 600,
          valuationRate: 50,
          barcodes: ['8901234567890'],
        }),
      ),
    };
    const toasterMock = { warn: vi.fn(), error: vi.fn() };
    const routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: Router, useValue: routerMock },
        { provide: StockBalanceService, useValue: stockBalanceMock },
        { provide: ItemService, useValue: { getList: () => of({ items: [] }) } },
        { provide: WarehouseService, useValue: { getList: () => of({ items: [] }) } },
        { provide: CompanyService, useValue: { getList: () => of({ items: [] }) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QuickStockBalanceComponent());
    comp.filters.patchValue({
      warehouseId: 'wh-1',
      date: '2026-06-15',
      barcode: '8901234567890',
    });
    comp.onBarcodeChange();

    expect(stockBalanceMock.getQuickStockBalance).toHaveBeenCalledWith({
      warehouseId: 'wh-1',
      date: '2026-06-15',
      itemId: null,
      barcode: '8901234567890',
    });
    expect(comp.filters.get('itemId')?.value).toBe('item-1');
    expect(comp.result()?.qty).toBe(12);
    expect(comp.result()?.value).toBe(600);
  });

  it('warns when warehouse or date is missing and navigates to stock balance report', () => {
    const toasterMock = { warn: vi.fn(), error: vi.fn() };
    const routerMock = { navigate: vi.fn() };
    const stockBalanceMock = { getQuickStockBalance: vi.fn() };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: Router, useValue: routerMock },
        { provide: StockBalanceService, useValue: stockBalanceMock },
        { provide: ItemService, useValue: { getList: () => of({ items: [] }) } },
        { provide: WarehouseService, useValue: { getList: () => of({ items: [] }) } },
        { provide: CompanyService, useValue: { getList: () => of({ items: [] }) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QuickStockBalanceComponent());
    comp.filters.patchValue({ warehouseId: '', itemId: 'item-1' });
    comp.onItemChange();
    expect(toasterMock.warn).toHaveBeenCalledTimes(1);
    expect(stockBalanceMock.getQuickStockBalance).not.toHaveBeenCalled();

    comp.filters.patchValue({ warehouseId: 'wh-1', itemId: 'item-1' });
    comp.openStockBalanceReport();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/inventory/reports/stock-balance'], {
      queryParams: { itemId: 'item-1', warehouseId: 'wh-1' },
    });
  });
});
