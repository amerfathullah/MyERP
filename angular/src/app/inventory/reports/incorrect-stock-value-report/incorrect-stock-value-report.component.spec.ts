import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  IncorrectStockValueReportComponent,
  buildIncorrectStockValueCsvRows,
} from './incorrect-stock-value-report.component';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('IncorrectStockValueReportComponent', () => {
  it('formats incorrect stock value rows for CSV export', () => {
    const csv = buildIncorrectStockValueCsvRows([
      {
        postingDate: '2026-03-01T00:00:00Z',
        voucherType: 'StockEntry',
        voucherNo: 'STE-009',
        itemCode: 'ITM-2',
        warehouseName: 'WH 2',
        expectedStockValue: 800,
        stockValue: 1300,
        differenceValue: 500,
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Expected Stock Value']).toBe('800.00');
    expect(csv[0]['Stock Value']).toBe('1300.00');
    expect(csv[0]['Difference Value']).toBe('500.00');
  });

  it('calls getIncorrectStockValue with companyId, accountId, and fromDate', () => {
    const svcMock = {
      getIncorrectStockValue: vi.fn().mockReturnValue(of({
        companyId: 'c1',
        accountId: 'a1',
        resolvedFromDate: '2026-02-01',
        rows: [
          {
            stockLedgerEntryId: 'sle1',
            itemCode: 'ITM-2',
            expectedStockValue: 800,
            stockValue: 1300,
            differenceValue: 500,
          },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: StockDiagnosticsService, useValue: svcMock },
        { provide: AccountService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'a1', accountCode: '1140', accountName: 'Inventory' }] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new IncorrectStockValueReportComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ fromDate: '2026-02-01' });
    comp.generate();

    expect(svcMock.getIncorrectStockValue).toHaveBeenLastCalledWith({
      companyId: 'c1',
      accountId: 'a1',
      fromDate: '2026-02-01',
    });
    expect(comp.report()?.rows?.[0]?.differenceValue).toBe(500);
  });
});
