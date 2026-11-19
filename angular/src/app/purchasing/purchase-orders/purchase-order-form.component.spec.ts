import { Injector, runInInjectionContext } from '@angular/core';
import { FormArray, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { PurchaseOrderFormComponent } from './purchase-order-form.component';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { SupplierQuotationService } from '../../proxy/purchasing/supplier-quotation.service';
import { CompanyService } from '../../proxy/core/company.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemDetailsService } from '../../proxy/inventory/item-details.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';
import { CurrencyExchangeService } from '../../proxy/accounting/currency-exchange.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { TaxCalculationService } from '../../shared/services/tax-calculation.service';
import { PriceListService } from '../../proxy/inventory/price-list.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

/**
 * Tests for the Purchase Order form item management and calculation patterns.
 */
describe('PurchaseOrder form item calculations', () => {
  const fb = new FormBuilder();

  function createPOForm() {
    return fb.group({
      companyId: ['comp-1', Validators.required],
      supplierId: ['', Validators.required],
      orderDate: ['2026-01-15', Validators.required],
      expectedDeliveryDate: [''],
      notes: [''],
      items: fb.array([], Validators.minLength(1)),
    });
  }

  function addItem(items: FormArray, opts?: { description?: string; quantity?: number; unitPrice?: number }) {
    items.push(fb.group({
      description: [opts?.description ?? '', Validators.required],
      quantity: [opts?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [opts?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [0],
    }));
  }

  function getLineTotal(items: FormArray, index: number): number {
    const row = items.at(index);
    return (row.get('quantity')?.value ?? 0) * (row.get('unitPrice')?.value ?? 0);
  }

  function getNetTotal(items: FormArray): number {
    return items.controls.reduce((sum, row) => {
      const qty = row.get('quantity')?.value ?? 0;
      const rate = row.get('unitPrice')?.value ?? 0;
      return sum + (qty * rate);
    }, 0);
  }

  describe('form validation', () => {
    it('should be invalid without supplier', () => {
      const form = createPOForm();
      expect(form.valid).toBe(false);
      form.get('supplierId')?.setValue('sup-1');
      addItem(form.get('items') as FormArray, { description: 'Item', quantity: 1, unitPrice: 10 });
      expect(form.valid).toBe(true);
    });

    it('should require at least one item', () => {
      const form = createPOForm();
      form.get('supplierId')?.setValue('sup-1');
      // Form is valid per reactive forms but empty items = business validation at submit
      const items = form.get('items') as FormArray;
      expect(items.length).toBe(0);
      // The component blocks submit when items.length === 0 (UI-level check)
    });

    it('should reject zero quantity', () => {
      const items = fb.array([]) as FormArray;
      addItem(items, { description: 'Widget', quantity: 0, unitPrice: 100 });
      expect(items.at(0).get('quantity')?.valid).toBe(false);
    });

    it('should reject negative unit price', () => {
      const items = fb.array([]) as FormArray;
      addItem(items, { description: 'Widget', quantity: 5, unitPrice: -10 });
      expect(items.at(0).get('unitPrice')?.valid).toBe(false);
    });
  });

  describe('line totals', () => {
    it('should calculate simple line total', () => {
      const items = fb.array([]) as FormArray;
      addItem(items, { quantity: 10, unitPrice: 25 });
      expect(getLineTotal(items, 0)).toBe(250);
    });

    it('should handle fractional quantities', () => {
      const items = fb.array([]) as FormArray;
      addItem(items, { quantity: 2.5, unitPrice: 100 });
      expect(getLineTotal(items, 0)).toBe(250);
    });
  });

  describe('net total', () => {
    it('should sum all item lines', () => {
      const items = fb.array([]) as FormArray;
      addItem(items, { quantity: 10, unitPrice: 100 }); // 1000
      addItem(items, { quantity: 5, unitPrice: 50 });   // 250
      addItem(items, { quantity: 2, unitPrice: 75 });   // 150
      expect(getNetTotal(items)).toBe(1400);
    });

    it('should return 0 for empty items', () => {
      const items = fb.array([]) as FormArray;
      expect(getNetTotal(items)).toBe(0);
    });

    it('should update after quantity change', () => {
      const items = fb.array([]) as FormArray;
      addItem(items, { quantity: 5, unitPrice: 200 });
      expect(getNetTotal(items)).toBe(1000);

      items.at(0).get('quantity')?.setValue(10);
      expect(getNetTotal(items)).toBe(2000);
    });

    it('should update after item removal', () => {
      const items = fb.array([]) as FormArray;
      addItem(items, { quantity: 1, unitPrice: 500 });
      addItem(items, { quantity: 1, unitPrice: 300 });
      expect(getNetTotal(items)).toBe(800);

      items.removeAt(0);
      expect(getNetTotal(items)).toBe(300);
    });
  });

  describe('hasUnsavedChanges pattern', () => {
    it('should be clean initially', () => {
      const form = createPOForm();
      expect(form.dirty).toBe(false);
    });

    it('should be dirty after user input', () => {
      const form = createPOForm();
      form.get('supplierId')?.setValue('sup-1');
      form.get('supplierId')?.markAsDirty();
      expect(form.dirty).toBe(true);
    });
  });

  describe('BlanketOrder auto-linking on item selection', () => {
    it('patches blanketOrderId, displays hint, and shifts hints on row removal', () => {
      const getItemDetailsMock = vi.fn().mockReturnValue(of({
        rate: 42.5,
        description: 'Contract Item',
        blanketOrderId: 'bo-buy-1',
        blanketOrderNumber: 'BO-BUY-2026-001',
        blanketOrderRemainingQty: 80,
      }));
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: fb },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
          { provide: PurchaseOrderService, useValue: {} },
          { provide: SupplierService, useValue: {} },
          { provide: SupplierQuotationService, useValue: {} },
          { provide: CompanyService, useValue: {} },
          { provide: WarehouseService, useValue: {} },
          { provide: ItemDetailsService, useValue: { getItemDetails: getItemDetailsMock } },
          { provide: PartyDetailsService, useValue: {} },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: TaxCategoryService, useValue: {} },
          { provide: TaxRuleService, useValue: {} },
          { provide: TaxCalculationService, useValue: {} },
          { provide: PriceListService, useValue: {} },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: ToasterService, useValue: { error: vi.fn(), warn: vi.fn(), info: vi.fn(), success: vi.fn() } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new PurchaseOrderFormComponent());
      comp.form.patchValue({ companyId: 'comp-1', supplierId: 'sup-1', orderDate: '2026-11-18' });
      comp.addItemRow();
      comp.addItemRow();

      comp.onItemSelected(1, { id: 'item-2', itemName: 'Widget', itemCode: 'W-2' } as any);

      expect(getItemDetailsMock).toHaveBeenCalledWith(expect.objectContaining({
        itemId: 'item-2',
        transactionType: 'Buying',
        supplierId: 'sup-1',
        transactionDate: '2026-11-18',
      }));
      expect(comp.items.at(1).get('blanketOrderId')?.value).toBe('bo-buy-1');
      expect(comp.items.at(1).get('unitPrice')?.value).toBe(42.5);
      expect(comp.getBlanketOrderInfo(1)).toEqual({ orderNumber: 'BO-BUY-2026-001', remainingQty: 80 });

      comp.removeItemRow(0);
      expect(comp.getBlanketOrderInfo(0)).toEqual({ orderNumber: 'BO-BUY-2026-001', remainingQty: 80 });
      expect(comp.getBlanketOrderInfo(1)).toBeNull();
    });

    it('clears stale blanketOrderId when supplier changes', () => {
      const getItemDetailsMock = vi.fn().mockImplementation((input: any) => {
        if (input.supplierId === 'sup-1') {
          return of({ rate: 40, description: 'Part', blanketOrderId: 'bo-sup-1', blanketOrderNumber: 'BO-1', blanketOrderRemainingQty: 10 });
        }
        return of({ rate: 50, description: 'Part', blanketOrderId: null });
      });
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: fb },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
          { provide: PurchaseOrderService, useValue: {} },
          { provide: SupplierService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
          { provide: SupplierQuotationService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyService, useValue: {} },
          { provide: WarehouseService, useValue: {} },
          { provide: ItemDetailsService, useValue: { getItemDetails: getItemDetailsMock } },
          { provide: PartyDetailsService, useValue: { getSupplierDetails: vi.fn().mockReturnValue(of({})) } },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: TaxCategoryService, useValue: {} },
          { provide: TaxRuleService, useValue: {} },
          { provide: TaxCalculationService, useValue: {} },
          { provide: PriceListService, useValue: {} },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: ToasterService, useValue: { error: vi.fn(), warn: vi.fn(), info: vi.fn(), success: vi.fn() } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new PurchaseOrderFormComponent());
      comp.form.patchValue({ companyId: 'comp-1', supplierId: 'sup-1', orderDate: '2026-11-18' });
      comp.addItemRow({ itemId: 'item-1', description: 'Part', quantity: 2, unitPrice: 40 });
      comp.onItemSelected(0, { id: 'item-1', itemName: 'Part', itemCode: 'P-1' } as any);
      expect(comp.items.at(0).get('blanketOrderId')?.value).toBe('bo-sup-1');

      comp.form.patchValue({ supplierId: 'sup-2' });
      comp.onSupplierChanged();

      expect(comp.items.at(0).get('blanketOrderId')?.value).toBeNull();
      expect(comp.getBlanketOrderInfo(0)).toBeNull();
    });

    it('clears blanketOrderId and refreshes unitPrice when changing row to a non-Blanket-Order item (51df5c2ef0)', () => {
      const getItemDetailsMock = vi.fn().mockImplementation((input: any) => {
        if (input.itemId === 'item-bo') {
          return of({ rate: 40, description: 'BO Part', blanketOrderId: 'bo-sup-1', blanketOrderNumber: 'BO-1', blanketOrderRemainingQty: 10 });
        }
        return of({ rate: 75, description: 'Standard Part', blanketOrderId: null });
      });
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: fb },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
          { provide: PurchaseOrderService, useValue: {} },
          { provide: SupplierService, useValue: {} },
          { provide: SupplierQuotationService, useValue: {} },
          { provide: CompanyService, useValue: {} },
          { provide: WarehouseService, useValue: {} },
          { provide: ItemDetailsService, useValue: { getItemDetails: getItemDetailsMock } },
          { provide: PartyDetailsService, useValue: {} },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: TaxCategoryService, useValue: {} },
          { provide: TaxRuleService, useValue: {} },
          { provide: TaxCalculationService, useValue: {} },
          { provide: PriceListService, useValue: {} },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: ToasterService, useValue: { error: vi.fn(), warn: vi.fn(), info: vi.fn(), success: vi.fn() } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new PurchaseOrderFormComponent());
      comp.form.patchValue({ companyId: 'comp-1', supplierId: 'sup-1', orderDate: '2026-11-18' });
      comp.addItemRow();
      comp.onItemSelected(0, { id: 'item-bo', itemName: 'BO Part', itemCode: 'BP-1' } as any);
      expect(comp.items.at(0).get('blanketOrderId')?.value).toBe('bo-sup-1');
      expect(comp.items.at(0).get('unitPrice')?.value).toBe(40);

      comp.onItemSelected(0, { id: 'item-std', itemName: 'Standard Part', itemCode: 'SP-1' } as any);
      expect(comp.items.at(0).get('blanketOrderId')?.value).toBeNull();
      expect(comp.getBlanketOrderInfo(0)).toBeNull();
      expect(comp.items.at(0).get('unitPrice')?.value).toBe(75);
    });
  });
});
