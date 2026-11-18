import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  FifoQueueVsQtyComparisonComponent,
  buildFifoComparisonCsvRows,
} from './fifo-queue-vs-qty-comparison.component';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('FifoQueueVsQtyComparisonComponent', () => {
  it('formats FIFO queue comparison rows for CSV export', () => {
    const csv = buildFifoComparisonCsvRows([
      {
        itemCode: 'FIFO-1',
        warehouseName: 'Stores',
        postingDate: '2026-06-02T00:00:00Z',
        voucherType: 'StockEntry',
        voucherNo: 'STE-02',
        actualQty: 5,
        qtyAfterTransaction: 7,
        fifoQueueQty: 10,
        fifoQtyDiff: -3,
        stockValue: 700,
        fifoStockValue: 1000,
        fifoValueDiff: -300,
        valuationRate: 100,
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Qty After Transaction']).toBe(7);
    expect(csv[0]['FIFO Queue Qty']).toBe(10);
    expect(csv[0]['Qty Diff']).toBe(-3);
  });

  it('calls getFifoQueueVsQtyComparison when item, warehouse, or group filter is provided', () => {
    const svcMock = {
      getFifoQueueVsQtyComparison: vi.fn().mockReturnValue(of({
        rows: [
          { stockLedgerEntryId: 'sle1', itemCode: 'FIFO-1', fifoQtyDiff: -3 },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: StockDiagnosticsService, useValue: svcMock },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ItemGroupService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn(), warn: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new FifoQueueVsQtyComparisonComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ itemId: 'i1', warehouseId: 'w1' });
    comp.generate();

    expect(svcMock.getFifoQueueVsQtyComparison).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      itemId: 'i1',
      warehouseId: 'w1',
    }));
    expect(comp.report()?.rows).toHaveLength(1);
  });
});
