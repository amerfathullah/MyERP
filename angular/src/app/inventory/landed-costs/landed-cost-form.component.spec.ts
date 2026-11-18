import { Injector, runInInjectionContext } from '@angular/core';
import { Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { LandedCostFormComponent } from './landed-cost-form.component';
import { LandedCostVoucherService } from '../../proxy/inventory';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import { ProjectService } from '../../proxy/projects/project.service';

describe('LandedCostFormComponent Distribution Preview', () => {
  function createComponent() {
    const lcvServiceMock = {
      getEligibleReceipts: vi.fn().mockImplementation(({ receiptType }: any) => {
        if (receiptType === 'PurchaseInvoice') {
          return of([{ id: 'pi-1', receiptType: 'PurchaseInvoice', documentNumber: 'PINV-001' }]);
        }
        if (receiptType === 'StockEntry') {
          return of([{ id: 'se-1', receiptType: 'StockEntry', documentNumber: 'STE-001' }]);
        }
        return of([{ id: 'pr-1', receiptType: 'PurchaseReceipt', documentNumber: 'MAT-PRE-001' }]);
      }),
      calculateDistribution: vi.fn().mockReturnValue(of({
        totalCharges: 300,
        totalDistributedAmount: 300,
        distributedItems: [
          { receiptId: 'pr-1', itemId: 'item-1', applicableCharges: 100 },
          { receiptId: 'pr-1', itemId: 'item-2', applicableCharges: 200 },
        ],
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: LandedCostVoucherService, useValue: lcvServiceMock },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: AccountService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CostCenterService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ProjectService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new LandedCostFormComponent());
    return { comp, lcvServiceMock };
  }

  it('calls calculateDistribution and updates applicableCharges per receipt row', () => {
    const { comp, lcvServiceMock } = createComponent();
    comp.form.distributionMethod = 1;
    comp.form.items = [
      { receiptId: 'pr-1', receiptType: 'PurchaseReceipt', itemId: 'item-1', quantity: 2, amount: 1000 },
      { receiptId: 'pr-1', receiptType: 'PurchaseReceipt', itemId: 'item-2', quantity: 4, amount: 2000 },
    ];
    comp.form.charges = [
      { expenseAccountId: 'acc-freight', description: 'Sea Freight', amount: 300, costCenterId: null, projectId: null },
    ];

    comp.previewDistribution();

    expect(lcvServiceMock.calculateDistribution).toHaveBeenCalledWith(
      expect.objectContaining({ distributionMethod: 1, totalCharges: 300 })
    );
    expect(comp.form.items[0].applicableCharges).toBe(100);
    expect(comp.form.items[1].applicableCharges).toBe(200);
  });

  it('loads eligible Purchase Invoices and Stock Entries via LandedCostVoucherService without PurchaseInvoiceService read permission', () => {
    const { comp, lcvServiceMock } = createComponent();

    comp.ngOnInit();

    expect(lcvServiceMock.getEligibleReceipts).toHaveBeenCalledWith({ companyId: 'comp-1', receiptType: 'PurchaseReceipt' });
    expect(lcvServiceMock.getEligibleReceipts).toHaveBeenCalledWith({ companyId: 'comp-1', receiptType: 'PurchaseInvoice' });
    expect(lcvServiceMock.getEligibleReceipts).toHaveBeenCalledWith({ companyId: 'comp-1', receiptType: 'StockEntry' });

    expect(comp.filteredReceipts().map(r => r.id)).toEqual(['pr-1']);

    comp.loadReceiptType = 'PurchaseInvoice';
    expect(comp.filteredReceipts().map(r => r.number)).toEqual(['PINV-001']);

    comp.loadReceiptType = 'StockEntry';
    expect(comp.filteredReceipts().map(r => r.number)).toEqual(['STE-001']);
  });
});
