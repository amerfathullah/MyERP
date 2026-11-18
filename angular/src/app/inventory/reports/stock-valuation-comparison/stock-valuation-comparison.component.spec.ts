import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CompanyService } from '../../../proxy/core/company.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import {
  StockValuationComparisonComponent,
  buildStockValuationComparisonCsvRows,
} from './stock-valuation-comparison.component';

describe('StockValuationComparisonComponent', () => {
  it('formats stock valuation comparison rows for CSV export', () => {
    const csv = buildStockValuationComparisonCsvRows([
      {
        stockLedgerEntryId: 'sle-1',
        postingDate: '2026-06-02T00:00:00Z',
        postingTime: '10:00:00',
        itemCode: 'MA-DRIFT',
        warehouseName: 'Main WH',
        voucherType: 'PurchaseReceipt',
        voucherNo: 'PR-001',
        actualQty: 10,
        qtyAfterTransaction: 20,
        expectedQtyAfterTransaction: 20,
        qtyDifference: 0,
        stockValueDifference: 1400,
        expectedStockValueDifference: 2000,
        stockValueDifferenceDifference: -600,
        valuationRate: 120,
        expectedValuationRate: 150,
        valuationRateDifference: -30,
        stockValue: 2400,
        expectedStockValue: 3000,
        stockValueDifferenceInBalance: -600,
        expectedBasis: 'Incoming rate',
        hasDifference: true,
      },
    ]);

    expect(csv).toHaveLength(1);
    expect(csv[0]['Item Code']).toBe('MA-DRIFT');
    expect(csv[0]['Expected Valuation Rate']).toBe('150.0000');
    expect(csv[0]['Balance Value Diff']).toBe('-600.00');
    expect(csv[0]['Expected Value Based On']).toBe('Incoming rate');
  });

  it('calls getStockValuationComparison and createStockValuationReposts', () => {
    const diagnosticsMock = {
      getStockValuationComparison: vi.fn().mockReturnValue(
        of({
          companyId: 'co-1',
          differenceCount: 1,
          totalEntriesEvaluated: 2,
          rows: [
            {
              stockLedgerEntryId: 'sle-1',
              hasDifference: true,
              valuationRateDifference: -30,
            },
          ],
        }),
      ),
      createStockValuationReposts: vi.fn().mockReturnValue(of({ created: [{ repostId: 'rep-1' }], skipped: [] })),
    };
    const toasterMock = { warn: vi.fn(), error: vi.fn(), info: vi.fn(), success: vi.fn() };

    const injector = Injector.create({
      providers: [
        FormBuilder,
        { provide: StockDiagnosticsService, useValue: diagnosticsMock },
        { provide: ItemService, useValue: { getList: () => of({ items: [] }) } },
        { provide: ItemGroupService, useValue: { getList: () => of({ items: [] }) } },
        { provide: WarehouseService, useValue: { getList: () => of({ items: [] }) } },
        { provide: CompanyService, useValue: { getList: () => of({ items: [] }) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockValuationComparisonComponent());
    comp.filters.patchValue({ companyId: 'co-1' });
    comp.generate();

    expect(diagnosticsMock.getStockValuationComparison).toHaveBeenCalledTimes(1);
    expect(comp.report()?.differenceCount).toBe(1);

    comp.repostSelected();
    expect(diagnosticsMock.createStockValuationReposts).toHaveBeenCalledWith({
      companyId: 'co-1',
      stockLedgerEntryIds: ['sle-1'],
    });
  });
});
