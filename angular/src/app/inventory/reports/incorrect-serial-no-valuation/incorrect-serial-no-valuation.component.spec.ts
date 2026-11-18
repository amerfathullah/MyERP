import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  IncorrectSerialNoValuationComponent,
  buildIncorrectSerialNoValuationCsvRows,
} from './incorrect-serial-no-valuation.component';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('IncorrectSerialNoValuationComponent', () => {
  it('formats incorrect serial no valuation rows for CSV export', () => {
    const csv = buildIncorrectSerialNoValuationCsvRows([
      {
        postingDate: '2026-06-01T00:00:00Z',
        voucherType: 'StockEntry',
        voucherNo: 'STE-10',
        itemCode: 'SER-1',
        warehouseName: 'Stores',
        serialNoNumber: 'SN-001',
        qty: -1,
        valuationRate: -60,
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Serial No']).toBe('SN-001');
    expect(csv[0]['Valuation Rate']).toBe('-60.0000');
  });

  it('calls getIncorrectSerialNoValuation with filters', () => {
    const svcMock = {
      getIncorrectSerialNoValuation: vi.fn().mockReturnValue(of({
        rows: [
          { serialNoNumber: 'Balance', qty: 0, valuationRate: 40, isBalanceRow: true },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: StockDiagnosticsService, useValue: svcMock },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'i1', itemCode: 'SER-1' }] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new IncorrectSerialNoValuationComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ itemId: 'i1' });
    comp.generate();

    expect(svcMock.getIncorrectSerialNoValuation).toHaveBeenLastCalledWith(expect.objectContaining({
      companyId: 'c1',
      itemId: 'i1',
    }));
    expect(comp.report()?.rows?.[0]?.valuationRate).toBe(40);
  });
});
