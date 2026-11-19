import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { AssetRepairFormComponent } from './asset-repair-form.component';
import { AssetRepairService } from '../../proxy/assets/asset-repair.service';
import { AssetService } from '../../proxy/assets/asset.service';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ProjectService } from '../../proxy/projects/project.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

function createForm(routeId: string | null = null, serviceOverrides: Record<string, any> = {}) {
  const repairServiceMock = {
    get: vi.fn().mockReturnValue(of({})),
    create: vi.fn().mockReturnValue(of({ id: 'rep-new' })),
    update: vi.fn().mockReturnValue(of({ id: routeId ?? 'rep-1' })),
    getExpenseAccountsForPurchaseInvoice: vi.fn().mockReturnValue(of([])),
    getConsumedItemValuationRate: vi.fn().mockReturnValue(of(15)),
    ...serviceOverrides,
  };
  const routerMock = { navigate: vi.fn() };
  const toasterMock = { success: vi.fn(), error: vi.fn() };

  const injector = Injector.create({
    providers: [
      FormBuilder,
      { provide: AssetRepairService, useValue: repairServiceMock },
      { provide: AssetService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'ast-1', assetName: 'CNC', status: 1 }] })) } },
      { provide: CostCenterService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
      { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'item-1', itemCode: 'P-1', itemName: 'Bearing', maintainStock: true }] })) } },
      { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'wh-1', name: 'Main Store', isGroup: false }] })) } },
      { provide: ProjectService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
      { provide: PurchaseInvoiceService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
      { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => routeId } } } },
      { provide: Router, useValue: routerMock },
      { provide: ToasterService, useValue: toasterMock },
    ],
  });

  const comp = runInInjectionContext(injector, () => new AssetRepairFormComponent());
  comp.ngOnInit();
  return { comp, repairServiceMock, routerMock, toasterMock };
}

describe('AssetRepairFormComponent consumed stock items & FIFO rate lookup', () => {
  it('fetches outgoing valuation rate on item, warehouse, qty, and completionDate changes', () => {
    const { comp, repairServiceMock } = createForm();
    comp.form.patchValue({ assetId: 'ast-1', completionDate: '2026-11-18' });

    comp.addStockItemRow();
    comp.onStockItemChange(0, 'item-1');
    expect(repairServiceMock.getConsumedItemValuationRate).not.toHaveBeenCalled();

    comp.onStockWarehouseChange(0, 'wh-1');
    expect(repairServiceMock.getConsumedItemValuationRate).toHaveBeenCalledWith({
      itemId: 'item-1',
      warehouseId: 'wh-1',
      qty: 1,
      postingDate: '2026-11-18',
      serialAndBatchBundleId: null,
    });
    expect(comp.stockItems()[0].valuationRate).toBe(15);
    expect(comp.consumedItemsCost()).toBe(15);

    repairServiceMock.getConsumedItemValuationRate.mockReturnValue(of(18));
    comp.onStockQtyChange(0, 4);
    expect(repairServiceMock.getConsumedItemValuationRate).toHaveBeenLastCalledWith({
      itemId: 'item-1',
      warehouseId: 'wh-1',
      qty: 4,
      postingDate: '2026-11-18',
      serialAndBatchBundleId: null,
    });
    expect(comp.stockItems()[0].valuationRate).toBe(18);
    expect(comp.consumedItemsCost()).toBe(72);

    repairServiceMock.getConsumedItemValuationRate.mockReturnValue(of(12));
    comp.form.get('completionDate')?.setValue('2026-11-10');
    expect(repairServiceMock.getConsumedItemValuationRate).toHaveBeenLastCalledWith({
      itemId: 'item-1',
      warehouseId: 'wh-1',
      qty: 4,
      postingDate: '2026-11-10',
      serialAndBatchBundleId: null,
    });
    expect(comp.stockItems()[0].valuationRate).toBe(12);
    expect(comp.consumedItemsCost()).toBe(48);
  });

  it('does not overwrite valuation rate when serialAndBatchBundleId is present', () => {
    const { comp, repairServiceMock } = createForm();
    comp.stockItems.set([{
      itemId: 'item-1',
      itemName: 'Bearing',
      warehouseId: 'wh-1',
      qty: 2,
      valuationRate: 25,
      serialAndBatchBundleId: 'sabb-1',
    }]);

    comp.onStockQtyChange(0, 3);
    comp.form.get('completionDate')?.setValue('2026-11-12');
    expect(repairServiceMock.getConsumedItemValuationRate).not.toHaveBeenCalled();
    expect(comp.stockItems()[0].valuationRate).toBe(25);
  });

  it('sends valid stockItems on save and hydrates existing stockItems on edit', () => {
    const { comp, repairServiceMock } = createForm('rep-edit-1', {
      get: vi.fn().mockReturnValue(of({
        id: 'rep-edit-1',
        assetId: 'ast-1',
        failureDate: '2026-11-01T00:00:00Z',
        completionDate: '2026-11-02T00:00:00Z',
        repairCost: 50,
        capitalizeRepairCost: true,
        increaseInAssetLife: 3,
        stockItems: [
          { id: 'si-1', itemId: 'item-1', itemName: 'Bearing', warehouseId: 'wh-1', qty: 2, valuationRate: 15 },
        ],
        invoices: [],
      })),
    });

    expect(comp.stockItems().length).toBe(1);
    expect(comp.stockItems()[0].valuationRate).toBe(15);
    expect(repairServiceMock.getConsumedItemValuationRate).not.toHaveBeenCalled();

    comp.save();
    expect(repairServiceMock.update).toHaveBeenCalledTimes(1);
    const payload = repairServiceMock.update.mock.calls[0][1];
    expect(payload.stockItems).toEqual([
      {
        id: 'si-1',
        itemId: 'item-1',
        itemName: 'Bearing',
        warehouseId: 'wh-1',
        qty: 2,
        valuationRate: 15,
        serialAndBatchBundleId: null,
      },
    ]);
  });
});
