import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { StockGlComparisonComponent } from './stock-gl-comparison.component';
import { StockGlComparisonService } from '../../../proxy/inventory/stock-gl-comparison.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('StockGlComparisonComponent', () => {
  function createComponent(comparisonOverrides: Record<string, any> = {}) {
    const comparisonData = {
      totalStockValue: 500,
      totalGlBalance: 420,
      difference: 80,
      isMatched: false,
      warehouseCount: 1,
      itemCount: 2,
      asOfDate: '2026-11-16',
      perWarehouse: [],
      mismatchedVouchers: [
        {
          ledgerType: 'Stock Ledger Entry',
          voucherType: 'StockEntry',
          voucherId: 'se-1',
          voucherNumber: 'SE-0001',
          postingDate: '2026-11-10T00:00:00Z',
          stockValue: 200,
          glBalance: 120,
          difference: 80,
        },
        {
          ledgerType: 'GL Entry',
          voucherType: 'JournalEntry',
          voucherId: 'je-9',
          voucherNumber: 'JE-0009',
          postingDate: '2026-11-12T00:00:00Z',
          stockValue: 0,
          glBalance: 50,
          difference: -50,
        },
      ],
      ...comparisonOverrides,
    };

    const stockGlMock = {
      getComparison: vi.fn().mockReturnValue(of(comparisonData)),
      createGlRepostingEntries: vi.fn().mockReturnValue(of({
        createdCount: 1,
        skippedCount: 0,
        repostIds: ['rep-1'],
        message: 'GL reposting entries created: 1',
      })),
    };
    const companyMock = { getList: vi.fn().mockReturnValue(of({ items: [] })) };
    const companyCtxMock = { currentCompanyId: () => 'comp-1' };
    const toasterMock = { success: vi.fn(), warn: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: StockGlComparisonService, useValue: stockGlMock },
        { provide: CompanyService, useValue: companyMock },
        { provide: CompanyContextService, useValue: companyCtxMock },
        { provide: ToasterService, useValue: toasterMock },
        FormBuilder,
      ],
    });

    const comp = runInInjectionContext(injector, () => new StockGlComparisonComponent());
    comp.companyId.setValue('comp-1');
    return { comp, stockGlMock, toasterMock };
  }

  it('populates mismatchedVouchers and sets earliest posting date on runComparison', () => {
    const { comp, stockGlMock, toasterMock } = createComponent();

    comp.runComparison();

    expect(stockGlMock.getComparison).toHaveBeenCalled();
    expect(comp.result()?.mismatchedVouchers?.length).toBe(2);
    expect(comp.selectableVouchers().length).toBe(1);
    expect(comp.repostFromDate()).toBe('2026-11-10');
    expect(toasterMock.warn).toHaveBeenCalledWith('::StockGlMismatchDetected');
  });

  it('selects only Stock Ledger Entry rows and submits createGlRepostingEntries', () => {
    const { comp, stockGlMock, toasterMock } = createComponent();
    comp.runComparison();

    comp.toggleAllVouchers(true);
    expect(comp.selectedVoucherKeys().size).toBe(1);
    expect(comp.allSelectableSelected()).toBe(true);

    comp.createGlRepostingEntries();

    expect(stockGlMock.createGlRepostingEntries).toHaveBeenCalledWith({
      companyId: 'comp-1',
      fromDate: '2026-11-10',
      vouchers: [{ voucherType: 'StockEntry', voucherId: 'se-1' }],
    });
    expect(toasterMock.success).toHaveBeenCalledWith('GL reposting entries created: 1');
    expect(stockGlMock.getComparison).toHaveBeenCalledTimes(2);
  });
});
