import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { AssetFormComponent } from './asset-form.component';
import { AssetStore } from '../store/asset.store';
import { AssetService } from '../../proxy/assets/asset.service';
import { AssetCategoryService } from '../../proxy/assets/asset-category.service';
import { LocationService } from '../../proxy/assets/location.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';

describe('AssetFormComponent Purchase Doc Fetch', () => {
  function createComponent(queryParams: Record<string, string> = {}) {
    const assetServiceMock = {
      get: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      getValuesFromPurchaseDoc: vi.fn().mockReturnValue(of({
        companyId: 'comp-1',
        purchaseDate: '2026-10-15T00:00:00',
        purchaseAmount: 4500,
        assetQuantity: 2,
        itemId: 'item-mac',
        itemName: 'MacBook Pro',
        locationId: 'loc-hq',
        costCenterId: 'cc-it',
        purchaseReceiptId: 'pr-1',
        purchaseReceiptItemId: 'pri-1',
      })),
    };
    const prServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'pr-1', receiptNumber: 'PR-2026-001' }] })),
    };
    const piServiceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'pi-1', invoiceNumber: 'PI-2026-009' }] })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const categoryMock = {
      getList: vi.fn().mockReturnValue(of({ items: [] })),
      getAccountForCompany: vi.fn().mockReturnValue(of({ fixedAssetAccountId: 'acc-fa-1' })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParams } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: AssetStore, useValue: {} },
        { provide: AssetService, useValue: assetServiceMock },
        { provide: AssetCategoryService, useValue: categoryMock },
        { provide: LocationService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: EmployeeService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PurchaseReceiptService, useValue: prServiceMock },
        { provide: PurchaseInvoiceService, useValue: piServiceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new AssetFormComponent());
    comp.ngOnInit();
    return { comp, assetServiceMock, categoryMock, prServiceMock, piServiceMock, toasterMock };
  }

  it('auto-fetches purchase doc values when queryParams provide purchaseDocType and purchaseDocId', () => {
    const { comp, assetServiceMock } = createComponent({
      purchaseDocType: 'PurchaseReceipt',
      purchaseDocId: 'pr-1',
      itemId: 'item-mac',
    });

    expect(assetServiceMock.getValuesFromPurchaseDoc).toHaveBeenCalledWith('PurchaseReceipt', 'pr-1', 'item-mac', undefined);
    expect(comp.form.get('purchaseAmount')?.value).toBe(4500);
    expect(comp.form.get('assetQuantity')?.value).toBe(2);
    expect(comp.form.get('assetName')?.value).toBe('MacBook Pro');
    expect(comp.form.get('purchaseReceiptId')?.value).toBe('pr-1');
    expect(comp.form.get('locationId')?.value).toBe('loc-hq');
  });

  it('switches purchase document list between PurchaseReceipt and PurchaseInvoice', () => {
    const { comp, piServiceMock, toasterMock, assetServiceMock } = createComponent();
    expect(comp.purchaseDocs()).toEqual([{ id: 'pr-1', number: 'PR-2026-001' }]);

    comp.onPurchaseDocTypeChange('PurchaseInvoice');
    expect(piServiceMock.getList).toHaveBeenCalled();
    expect(comp.purchaseDocs()).toEqual([{ id: 'pi-1', number: 'PI-2026-009' }]);

    comp.purchaseDocId.set('pi-1');
    comp.fetchFromSelectedPurchaseDoc();
    expect(assetServiceMock.getValuesFromPurchaseDoc).toHaveBeenCalledWith('PurchaseInvoice', 'pi-1', undefined, undefined);
    expect(toasterMock.success).toHaveBeenCalled();
  });

  it('loads company GL account mapping when assetCategoryId changes', () => {
    const { comp, categoryMock } = createComponent();

    comp.form.patchValue({ assetCategoryId: 'cat-it' });

    expect(categoryMock.getAccountForCompany).toHaveBeenCalledWith('cat-it', 'comp-1');
    expect(comp.categoryAccount()?.fixedAssetAccountId).toBe('acc-fa-1');
    expect(comp.categoryAccountMissing()).toBe(false);
  });
});
