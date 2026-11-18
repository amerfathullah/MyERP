import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import {
  IncorrectSerialAndBatchBundleComponent,
  buildIncorrectBundleCsvRows,
} from './incorrect-serial-and-batch-bundle.component';
import { StockDiagnosticsService } from '../../../proxy/inventory/stock-diagnostics.service';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';

describe('IncorrectSerialAndBatchBundleComponent', () => {
  it('formats incorrect serial/batch bundles for CSV export', () => {
    const csv = buildIncorrectBundleCsvRows([
      {
        bundleId: 'b1',
        itemCode: 'BND-1',
        warehouseName: 'Stores',
        voucherType: 'StockEntry',
        voucherNo: 'STE-01',
        issueType: 'UnlinkedBundle',
        isCancelled: false,
      },
    ]);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Issue Type']).toBe('UnlinkedBundle');
    expect(csv[0]['Is Cancelled']).toBe('No');
  });

  it('queries bundles and invokes fixIncorrectSerialAndBatchBundles for selected ids', () => {
    const svcMock = {
      getIncorrectSerialAndBatchBundles: vi.fn().mockReturnValue(of({
        rows: [
          { bundleId: 'b1', itemCode: 'BND-1', issueType: 'UnlinkedBundle', isCancelled: false },
        ],
      })),
      fixIncorrectSerialAndBatchBundles: vi.fn().mockReturnValue(of({
        cancelledCount: 1,
        restoredCount: 0,
        skippedCount: 0,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: StockDiagnosticsService, useValue: svcMock },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', name: 'Main Co' }] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'c1' } },
        { provide: ToasterService, useValue: { error: vi.fn(), success: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new IncorrectSerialAndBatchBundleComponent());
    comp.ngOnInit();
    expect(comp.report()?.rows).toHaveLength(1);

    comp.toggleSelection('b1');
    comp.fixSelected();

    expect(svcMock.fixIncorrectSerialAndBatchBundles).toHaveBeenCalledWith({ bundleIds: ['b1'] });
  });
});
