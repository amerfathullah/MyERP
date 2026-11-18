import { describe, it, expect, vi, beforeEach } from 'vitest';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { Injector, runInInjectionContext } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  AssetDepreciationLedgerComponent,
  filterDepreciationLedgerRows,
} from './asset-depreciation-ledger.component';
import { AssetService } from '../../../proxy/assets/asset.service';
import { AssetReportingService } from '../../../proxy/assets/asset-reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('AssetDepreciationLedgerComponent', () => {
  let assetMock: { getCategories: ReturnType<typeof vi.fn> };
  let reportingMock: { getAssetDepreciationLedger: ReturnType<typeof vi.fn> };
  let companyMock: { getList: ReturnType<typeof vi.fn> };
  let routerMock: { navigate: ReturnType<typeof vi.fn> };
  let comp: AssetDepreciationLedgerComponent;

  beforeEach(() => {
    assetMock = {
      getCategories: vi.fn().mockReturnValue(of([{ id: 'cat-1', categoryName: 'IT Equipment' }])),
    };
    reportingMock = {
      getAssetDepreciationLedger: vi.fn().mockReturnValue(of({
        companyId: 'comp-1',
        currencyCode: 'MYR',
        totalDepreciationAmount: 150,
        rows: [
          {
            assetId: 'ast-1',
            assetNumber: 'AST-001',
            assetName: 'Laptop Pro',
            assetCategoryName: 'IT Equipment',
            depreciationDate: '2026-10-31T00:00:00Z',
            grossPurchaseAmount: 1200,
            depreciationAmount: 100,
            accumulatedDepreciationAmount: 300,
            valueAfterDepreciation: 900,
            depreciationEntryId: 'je-1',
            depreciationEntryNumber: 'JE-DEP-1',
          },
          {
            assetId: 'ast-2',
            assetNumber: 'AST-002',
            assetName: 'Office Printer',
            assetCategoryName: 'Office',
            depreciationDate: '2026-11-30T00:00:00Z',
            grossPurchaseAmount: 600,
            depreciationAmount: 50,
            accumulatedDepreciationAmount: 150,
            valueAfterDepreciation: 450,
            depreciationEntryId: 'je-2',
            depreciationEntryNumber: 'JE-DEP-2',
          },
        ],
      })),
    };
    companyMock = { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', name: 'Test Co' }] })) };
    routerMock = { navigate: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: Router, useValue: routerMock },
        { provide: AssetService, useValue: assetMock },
        { provide: AssetReportingService, useValue: reportingMock },
        { provide: CompanyService, useValue: companyMock },
        {
          provide: CompanyContextService,
          useValue: { load: vi.fn(), currentCompanyId: () => 'comp-1' },
        },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    comp = runInInjectionContext(injector, () => new AssetDepreciationLedgerComponent());
  });

  it('loads categories/companies, computes total depreciation, and filters search results', () => {
    comp.ngOnInit();

    expect(reportingMock.getAssetDepreciationLedger).toHaveBeenCalledWith(expect.objectContaining({
      companyId: 'comp-1',
      includeDefaultBookAssets: true,
    }));
    expect(comp.filteredRows().length).toBe(2);
    expect(comp.totalDepreciation()).toBe(150);

    expect(filterDepreciationLedgerRows(comp.filteredRows(), 'laptop').length).toBe(1);
    comp.searchTerm.set('AST-002');
    expect(comp.filteredRows().length).toBe(1);
    expect(comp.totalDepreciation()).toBe(50);
  });

  it('navigates to asset detail or depreciation journal entry', () => {
    const row = { assetId: 'ast-1', depreciationEntryId: 'je-1' };
    comp.openAsset(row);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/assets', 'ast-1']);

    comp.openJournalEntry(row, { stopPropagation: vi.fn() } as unknown as Event);
    expect(routerMock.navigate).toHaveBeenCalledWith(['/accounting/journal-entries', 'je-1']);
  });
});
