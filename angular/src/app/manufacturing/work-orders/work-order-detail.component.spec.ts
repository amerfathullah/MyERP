import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { WorkOrderDetailComponent } from './work-order-detail.component';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { QualityInspectionService } from '../../proxy/inventory/quality-inspection.service';
import { InspectionStatus } from '../../proxy/inventory/inspection-status.enum';
import { StockReservationService } from '../../proxy/inventory/stock-reservation.service';

describe('WorkOrderDetailComponent BOM & Return Actions', () => {
  function createComponent(confirmStatus = Confirmation.Status.confirm) {
    const mfgMock = {
      getWorkOrder: vi.fn().mockReturnValue(of(null)),
      createBomFromWorkOrder: vi.fn().mockReturnValue(of({ id: 'bom-99' })),
      createMaterialReturnForManufacture: vi.fn().mockReturnValue(of({ stockEntryId: 'se-77' })),
      getFgConversionDetails: vi.fn().mockReturnValue(of({
        alternativeItems: [{ itemId: 'alt-1', itemCode: 'ALT-01', itemName: 'Alt Widget' }],
        availableQty: 4,
      })),
      createFgConversionEntry: vi.fn().mockReturnValue(of({ stockEntryId: 'se-fg-1' })),
    };
    const routerMock = { navigate: vi.fn() };
    const confirmationMock = { warn: vi.fn().mockReturnValue(of(confirmStatus)) };
    const toasterMock = { success: vi.fn(), error: vi.fn(), warn: vi.fn() };

    const itemMock = { get: vi.fn().mockReturnValue(of({})) };
    const qiMock = { getList: vi.fn().mockReturnValue(of({ items: [] })) };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: routerMock },
        { provide: ManufacturingService, useValue: mfgMock },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ItemService, useValue: itemMock },
        { provide: QualityInspectionService, useValue: qiMock },
        { provide: StockReservationService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new WorkOrderDetailComponent());
    return { comp, mfgMock, routerMock, confirmationMock, toasterMock, itemMock, qiMock };
  }

  it('detects returnable excess materials only when Completed or Closed', () => {
    const { comp } = createComponent();

    comp.wo.set({
      id: 'wo-1',
      status: 3, // InProcess
      requiredItems: [{ itemId: 'rm-1', transferredQuantity: 10, consumedQuantity: 6 }],
    } as any);
    expect(comp.hasReturnableMaterials).toBe(false);

    comp.wo.set({
      id: 'wo-1',
      status: 4, // Completed
      requiredItems: [{ itemId: 'rm-1', transferredQuantity: 10, consumedQuantity: 6 }],
    } as any);
    expect(comp.hasReturnableMaterials).toBe(true);

    comp.wo.set({
      id: 'wo-1',
      status: 7, // Closed
      requiredItems: [{ itemId: 'rm-1', transferredQuantity: 10, consumedQuantity: 10 }],
    } as any);
    expect(comp.hasReturnableMaterials).toBe(false);
  });

  it('aggregates Return Components pending qty per item across split warehouse rows (PR #67351)', () => {
    const { comp } = createComponent();

    // Two rows for same item: transfer went to row 1, consumption split across row 1 & row 2 -> net 0 in WIP
    comp.wo.set({
      id: 'wo-2',
      status: 4, // Completed
      requiredItems: [
        { itemId: 'rm-1', transferredQuantity: 10, consumedQuantity: 5 },
        { itemId: 'rm-1', transferredQuantity: 0, consumedQuantity: 5 },
      ],
    } as any);
    expect(comp.hasReturnableMaterials).toBe(false);

    // Partial return/consumption leaves net positive qty for rm-1 -> button stays visible
    comp.wo.set({
      id: 'wo-2',
      status: 7, // Closed
      requiredItems: [
        { itemId: 'rm-1', transferredQuantity: 10, consumedQuantity: 4 },
        { itemId: 'rm-1', transferredQuantity: 0, consumedQuantity: 4 },
      ],
    } as any);
    expect(comp.hasReturnableMaterials).toBe(true);
  });

  it('creates BOM from work order and navigates to BOM detail', () => {
    const { comp, mfgMock, routerMock, toasterMock } = createComponent();
    comp.wo.set({ id: 'wo-1', status: 4 } as any);

    comp.createBomFromWorkOrder();

    expect(mfgMock.createBomFromWorkOrder).toHaveBeenCalledWith('wo-1');
    expect(toasterMock.success).toHaveBeenCalled();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/manufacturing/bom/detail', 'bom-99']);
  });

  it('returns excess materials only after confirmation', () => {
    const cancelled = createComponent(Confirmation.Status.dismiss);
    cancelled.comp.wo.set({ id: 'wo-1', status: 4 } as any);
    cancelled.comp.returnExcessMaterials();
    expect(cancelled.mfgMock.createMaterialReturnForManufacture).not.toHaveBeenCalled();

    const confirmed = createComponent(Confirmation.Status.confirm);
    confirmed.comp.wo.set({ id: 'wo-1', status: 4 } as any);
    confirmed.comp.returnExcessMaterials();
    expect(confirmed.mfgMock.createMaterialReturnForManufacture).toHaveBeenCalledWith('wo-1');
    expect(confirmed.routerMock.navigate).toHaveBeenCalledWith(['/inventory/stock-entries', 'se-77']);
  });

  it('loads FG conversion alternatives, validates quantity cap, and creates conversion stock entry', () => {
    const { comp, mfgMock, routerMock, toasterMock } = createComponent();
    comp.wo.set({ id: 'wo-1', status: 4, producedQuantity: 5 } as any);

    comp.loadFgConversionDetails('wo-1');
    expect(comp.fgConversionDetails()?.alternativeItems).toHaveLength(1);

    comp.openFgConversionModal();
    expect(comp.showFgConversionModal()).toBe(true);
    expect(comp.fgConversionAltItemId()).toBe('alt-1');
    expect(comp.fgConversionQty()).toBe(4);

    // Reject qty exceeding availableQty
    comp.fgConversionQty.set(10);
    comp.submitFgConversion();
    expect(mfgMock.createFgConversionEntry).not.toHaveBeenCalled();

    comp.fgConversionQty.set(3);
    comp.submitFgConversion();
    expect(mfgMock.createFgConversionEntry).toHaveBeenCalledWith({
      workOrderId: 'wo-1',
      alternativeItemId: 'alt-1',
      quantity: 3,
    });
    expect(comp.showFgConversionModal()).toBe(false);
    expect(toasterMock.success).toHaveBeenCalled();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/inventory/stock-entries', 'se-fg-1']);
  });

  it('resolves Quality Inspection Accepted status via QualityInspectionService.getList', () => {
    const { comp, itemMock, qiMock } = createComponent();
    comp.wo.set({ id: 'wo-1', companyId: 'co-1', itemId: 'fg-1', status: 3 } as any);
    itemMock.get.mockReturnValue(of({ inspectionRequiredBeforeDelivery: true }));
    qiMock.getList.mockReturnValue(of({
      items: [
        { id: 'qi-other', referenceId: 'wo-other', status: InspectionStatus.Rejected },
        { id: 'qi-1', referenceId: 'wo-1', status: InspectionStatus.Accepted },
      ],
    }));

    (comp as any).loadQiStatus('wo-1', 'fg-1');

    expect(qiMock.getList).toHaveBeenCalledWith({
      companyId: 'co-1',
      itemId: 'fg-1',
      skipCount: 0,
      maxResultCount: 50,
    });
    expect(comp.qiRequired()).toBe(true);
    expect(comp.qiStatus()).toBe('Accepted');
  });

  it('uses Transfer Extra % for Material Transfer allowance and falls back to Overproduction % (PR #67352)', () => {
    const { comp } = createComponent();
    comp.wo.set({
      id: 'wo-10',
      quantity: 100,
      materialTransferred: 20,
      producedQuantity: 10,
      overproductionPercentage: 5,
      transferExtraMaterialsPercentage: 15,
    } as any);

    expect(comp.getAllowancePercentage('Material Transfer for Manufacture')).toBe(15);
    expect(comp.getAllowancePercentage('Manufacture')).toBe(5);
    expect(comp.allowedTransferQty).toBe(95); // 100 * 1.15 - 20
    expect(comp.allowedProductionQty).toBe(95); // 100 * 1.05 - 10

    // Falls back to overproductionPercentage when transferExtraMaterialsPercentage is 0
    comp.wo.set({
      id: 'wo-11',
      quantity: 100,
      materialTransferred: 0,
      overproductionPercentage: 8,
      transferExtraMaterialsPercentage: 0,
    } as any);
    expect(comp.getAllowancePercentage('Material Transfer for Manufacture')).toBe(8);
    expect(comp.allowedTransferQty).toBe(108);
  });
});
