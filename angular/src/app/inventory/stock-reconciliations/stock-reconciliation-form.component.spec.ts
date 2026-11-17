import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockReconciliationService } from '../../proxy/inventory';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { mapReconciliationPreviewRows, StockReconciliationFormComponent } from './stock-reconciliation-form.component';

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
});
