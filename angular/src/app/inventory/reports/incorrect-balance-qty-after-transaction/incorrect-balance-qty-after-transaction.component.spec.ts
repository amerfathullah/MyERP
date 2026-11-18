import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  IncorrectBalanceQtyAfterTransactionComponent,
  buildIncorrectBalanceQtyCsvRows,
} from './incorrect-balance-qty-after-transaction.component';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('IncorrectBalanceQtyAfterTransactionComponent', () => {
  it('formats incorrect balance qty rows for CSV export', () => {
    const csv = buildIncorrectBalanceQtyCsvRows([
      {
        postingDate: '2026-06-01T00:00:00Z',
        voucherType: 'StockEntry',
        voucherNo: 'STE-001',
        itemCode: 'ITM-1',
        warehouseName: 'Stores',
        expectedBalanceQty: 10,
        actualBalanceQty: 5,
        difference: 5,
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Voucher No']).toBe('STE-001');
    expect(csv[0]['Expected Balance Qty']).toBe('10.00');
    expect(csv[0]['Difference']).toBe('5.00');
  });

  it('calls getIncorrectBalanceQty with selected filters', () => {
    const svcMock = {
      getIncorrectBalanceQty: vi.fn().mockReturnValue(of({
        rows: [
          {
            stockLedgerEntryId: 'sle1',
            itemCode: 'ITM-1',
            expectedBalanceQty: 10,
            actualBalanceQty: 5,
            difference: 5,
          },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: StockDiagnosticsService, useValue: svcMock },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'i1', itemCode: 'ITM-1' }] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'w1', companyId: 'c1', name: 'Stores' }] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new IncorrectBalanceQtyAfterTransactionComponent());
    comp.ngOnInit();
    comp.filters.patchValue({ warehouseId: 'w1', itemId: 'i1' });
    comp.generate();

    expect(svcMock.getIncorrectBalanceQty).toHaveBeenLastCalledWith({
      companyId: 'c1',
      warehouseId: 'w1',
      itemId: 'i1',
      precision: 2,
    });
    expect(comp.report()?.rows).toHaveLength(1);
  });
});
