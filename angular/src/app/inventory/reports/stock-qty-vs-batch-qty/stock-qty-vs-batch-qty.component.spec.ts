import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { StockQtyVsBatchQtyComponent, buildStockQtyVsBatchCsvRows } from './stock-qty-vs-batch-qty.component';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('StockQtyVsBatchQtyComponent', () => {
  it('loads mismatches, reconciles selected bins, and exports CSV rows', () => {
    const sampleRows = [
      {
        itemId: 'i1',
        itemCode: 'B-001',
        itemName: 'Chemical X',
        warehouseId: 'w1',
        warehouseName: 'Stores',
        batchCount: 1,
        batchNumbers: 'BCH-001',
        batchQty: 10,
        stockQty: 7,
        difference: -3,
        isMismatch: true,
      },
    ];

    const diagMock = {
      getStockQtyVsBatchQty: vi.fn().mockReturnValue(of({
        rows: sampleRows,
        totalMismatches: 1,
        totalBatchQty: 10,
        totalStockQty: 7,
        totalDifference: -3,
      })),
      reconcileBatchStockQty: vi.fn().mockReturnValue(of({ updatedCount: 1, message: 'OK' })),
    };

    const injector = Injector.create({
      providers: [
        { provide: StockDiagnosticsService, useValue: diagMock },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warn: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockQtyVsBatchQtyComponent());
    comp.ngOnInit();

    expect(diagMock.getStockQtyVsBatchQty).toHaveBeenCalledWith(
      expect.objectContaining({ companyId: 'c1', showOnlyDifferences: true }),
    );
    expect(comp.report()?.totalMismatches).toBe(1);

    comp.toggleSelect(sampleRows[0]);
    expect(comp.selectedMismatchRows()).toHaveLength(1);

    comp.reconcileSelected();
    expect(diagMock.reconcileBatchStockQty).toHaveBeenCalledWith({
      items: [{ itemId: 'i1', warehouseId: 'w1' }],
    });

    const csv = buildStockQtyVsBatchCsvRows(sampleRows);
    expect(csv[0]['Batch Qty']).toBe('10.0000');
    expect(csv[0]['Difference']).toBe('-3.0000');
  });
});
