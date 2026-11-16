import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { BatchListComponent } from './batch-list.component';
import { BatchService } from '../../proxy/inventory/batch.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('BatchListComponent', () => {
  function createComponent(batchMock: any) {
    const injector = Injector.create({
      providers: [
        { provide: BatchService, useValue: batchMock },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'wh-1', name: 'Main WH', isGroup: false }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });
    return runInInjectionContext(injector, () => new BatchListComponent());
  }

  it('calls BatchService.getAvailableBatches and clears results cleanly', () => {
    const mockBatches = [{ batchId: 'b-1', batchNo: 'BATCH-001', balanceQuantity: 10, reservedQuantity: 2, availableQuantity: 8, isExpired: false }];
    const batchMock = {
      getList: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 })),
      getAvailableBatches: vi.fn().mockReturnValue(of(mockBatches)),
    };
    const comp = createComponent(batchMock);
    comp.ngOnInit();

    comp.filterItemId = 'item-1';
    comp.filterWarehouseId = 'wh-1';
    comp.checkAvailability();

    expect(batchMock.getAvailableBatches).toHaveBeenCalledWith({
      companyId: 'comp-1',
      itemId: 'item-1',
      warehouseId: 'wh-1',
    });
    expect(comp.availableBatches()).toEqual(mockBatches);
    expect(comp.availableBatchesChecked()).toBe(true);

    comp.clearAvailability();
    expect(comp.filterItemId).toBe('');
    expect(comp.filterWarehouseId).toBe('');
    expect(comp.availableBatches()).toEqual([]);
    expect(comp.availableBatchesChecked()).toBe(false);
  });
});
