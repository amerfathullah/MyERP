import { ProductionPlanDetailComponent } from './production-plan-detail.component';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProductionPlanStore } from '../store/production-plan.store';
import { ProductionPlanService } from '../../proxy/manufacturing/production-plan.service';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

describe('ProductionPlanDetailComponent', () => {
  function createComponent(downloadObs = of([{
    itemCode: 'RM-01',
    itemName: 'Steel "Plate"',
    description: 'Raw steel',
    stockUom: 'Nos',
    warehouse: 'Main Stores',
    requiredBomQty: 10,
    projectedQty: 5,
    actualQty: 2,
    orderedQty: 0,
    plannedQty: 8,
    reservedQtyForProduction: 1,
    safetyStock: 2,
    requiredQty: 8,
    uom: 'Nos',
  }])) {
    const planServiceMock = {
      getSummaryReport: vi.fn().mockReturnValue(of({ rows: [] })),
      getVisualizerData: vi.fn().mockReturnValue(of({
        planId: 'plan-1',
        totalPlannedQty: 10,
        totalProducedQty: 5,
        completionPercentage: 50,
        finishedGoods: [],
        rawMaterials: [],
      })),
      downloadRawMaterials: vi.fn().mockReturnValue(downloadObs),
      getPooledBinDetails: vi.fn().mockReturnValue(of({ actualQty: 25, projectedQty: 40 })),
    };
    const storeMock = {
      selectedPlan: vi.fn().mockReturnValue({ id: 'plan-1', planNumber: 'PP-001', companyId: 'comp-1', forWarehouseId: 'wh-1', status: 1 }),
      isLoading: vi.fn().mockReturnValue(false),
      loadOne: vi.fn(),
      submit: vi.fn(),
      calculateMaterials: vi.fn(),
      generateWorkOrders: vi.fn(),
      generateMaterialRequests: vi.fn(),
      cancel: vi.fn(),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ProductionPlanStore, useValue: storeMock },
        { provide: ProductionPlanService, useValue: planServiceMock },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'plan-1' } } } },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ProductionPlanDetailComponent());
    return { comp, planServiceMock, toasterMock };
  }

  it('calls downloadRawMaterials proxy and exports CSV', () => {
    const { comp, planServiceMock } = createComponent();
    const clickMock = vi.fn();
    const origCreateElement = document.createElement.bind(document);
    const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      if (tag === 'a') {
        return { href: '', download: '', click: clickMock } as unknown as HTMLAnchorElement;
      }
      return origCreateElement(tag);
    });
    const origCreateObjectUrl = URL.createObjectURL;
    const origRevokeObjectUrl = URL.revokeObjectURL;
    URL.createObjectURL = vi.fn().mockReturnValue('blob:mock');
    URL.revokeObjectURL = vi.fn();

    try {
      comp.downloadRawMaterials();
      expect(planServiceMock.downloadRawMaterials).toHaveBeenCalledWith('plan-1');
      expect(clickMock).toHaveBeenCalled();
      expect(comp.downloadingMaterials()).toBe(false);
    } finally {
      createElementSpy.mockRestore();
      URL.createObjectURL = origCreateObjectUrl;
      URL.revokeObjectURL = origRevokeObjectUrl;
    }
  });

  it('loads pooled bin details for material requirement row', () => {
    const { comp, planServiceMock } = createComponent();
    comp.checkPooledBin({ itemId: 'rm-1' });

    expect(planServiceMock.getPooledBinDetails).toHaveBeenCalledWith('comp-1', 'rm-1', 'wh-1');
    expect(comp.pooledBins()['rm-1']).toEqual({ actualQty: 25, projectedQty: 40 });
    expect(comp.loadingBinItemId()).toBeNull();
  });

  it('shows toaster error when downloadRawMaterials fails', () => {
    const { comp, toasterMock } = createComponent(throwError(() => new Error('boom')));
    comp.downloadRawMaterials();
    expect(toasterMock.error).toHaveBeenCalled();
    expect(comp.downloadingMaterials()).toBe(false);
  });

  it('surfaces toaster error when summary or visualizer loading fails', () => {
    const { comp, planServiceMock, toasterMock } = createComponent();
    planServiceMock.getSummaryReport.mockReturnValue(throwError(() => ({ error: { error: { message: 'Summary load failed' } } })));
    planServiceMock.getVisualizerData.mockReturnValue(throwError(() => ({ error: { error: { message: 'Visualizer load failed' } } })));

    comp.ngOnInit();

    expect(comp.loadingSummary()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Summary load failed');
    expect(toasterMock.error).toHaveBeenCalledWith('Visualizer load failed');
  });
});
