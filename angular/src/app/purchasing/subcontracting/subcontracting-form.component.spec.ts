import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { SubcontractingFormComponent } from './subcontracting-form.component';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('SubcontractingFormComponent', () => {
  let mfgMock: any;

  function createComponent(): SubcontractingFormComponent {
    const injector = Injector.create({
      providers: [
        { provide: SubcontractingService, useValue: { createOrder: vi.fn().mockReturnValue(of({ id: 'sco-1' })) } },
        { provide: SupplierService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'fg-1', itemCode: 'FG-01', itemName: 'Assembly' }] })) } },
        { provide: ManufacturingService, useValue: mfgMock },
        { provide: CompanyContextService, useValue: { currentCompanyId: signal('comp-1') } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });
    return runInInjectionContext(injector, () => new SubcontractingFormComponent());
  }

  beforeEach(() => {
    mfgMock = {
      getBomItemsForSubcontracting: vi.fn().mockReturnValue(of({
        bomId: 'bom-9',
        bomNumber: 'BOM-009',
        sourceWarehouseId: 'wh-rm',
        items: [{ itemId: 'rm-1', itemCode: 'RM-01', itemName: 'Frame', requiredQty: 4, rate: 12 }],
      })),
    };
  });

  it('auto-resolves bomId, warehouseId, and raw material preview when finished good selected', () => {
    const comp = createComponent();
    comp.ngOnInit();
    comp.form.items[0].itemId = 'fg-1';
    comp.form.items[0].qty = 4;

    comp.onItemChange(0);

    expect(mfgMock.getBomItemsForSubcontracting).toHaveBeenCalledWith('fg-1', 'comp-1', 4);
    expect(comp.form.items[0].itemName).toBe('FG-01 - Assembly');
    expect(comp.form.items[0].bomId).toBe('bom-9');
    expect(comp.form.items[0].warehouseId).toBe('wh-rm');
    expect(comp.bomPreview()?.items).toHaveLength(1);
  });

  it('clears bomPreview on lookup error', () => {
    mfgMock.getBomItemsForSubcontracting.mockReturnValue(throwError(() => new Error('no bom')));
    const comp = createComponent();
    comp.form.items[0].itemId = 'fg-1';

    comp.onItemChange(0);

    expect(comp.bomPreview()).toBeNull();
  });
});
