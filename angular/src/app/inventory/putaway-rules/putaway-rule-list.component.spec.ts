import { Injector, runInInjectionContext } from '@angular/core';
import { LocalizationService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { PutawayRuleListComponent } from './putaway-rule-list.component';
import { PutawayRuleService } from '../../proxy/inventory/putaway-rule.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('PutawayRuleListComponent', () => {
  it('refreshes live available capacity via PutawayRuleService.getAvailableCapacity', () => {
    const putawayMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [{ id: 'rule-1', itemId: 'item-1', warehouseId: 'wh-1', stockCapacity: 100, availableCapacity: 40, priority: 1, isEnabled: true }],
      })),
      getAvailableCapacity: vi.fn().mockReturnValue(of(65)),
    };

    const injector = Injector.create({
      providers: [
        { provide: PutawayRuleService, useValue: putawayMock },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new PutawayRuleListComponent());
    comp.ngOnInit();

    expect(comp.items()[0].availableCapacity).toBe(40);
    comp.refreshCapacity('rule-1');

    expect(putawayMock.getAvailableCapacity).toHaveBeenCalledWith('rule-1');
    expect(comp.items()[0].availableCapacity).toBe(65);
    expect(comp.refreshingCapacityId()).toBeNull();
  });
});
