import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';
import { PurchaseInvoiceFormComponent } from './purchase-invoice-form.component';
import { TaxCalculationService } from '../../shared/services/tax-calculation.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { PaymentTermsTemplateService } from '../../proxy/accounting/payment-terms-template.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { TaxChargesTemplateService } from '../../proxy/tax/tax-charges-template.service';
import { CurrencyExchangeService } from '../../proxy/accounting/currency-exchange.service';
import { PriceListService } from '../../proxy/inventory/price-list.service';

describe('PurchaseInvoice form DTO mapping', () => {
  const fb = new FormBuilder();

  function createPIForm() {
    return fb.group({
      invoiceNumber: [''],
      companyId: ['comp-1', Validators.required],
      supplierId: ['', Validators.required],
      supplierTin: [''],
      issueDate: ['2026-07-20', Validators.required],
      dueDate: [''],
      currencyCode: ['MYR'],
      notes: [''],
      isReturn: [false],
      isDebitNote: [false],
      isReturnRefund: [false],
      eInvoiceDocType: [11 as number | null],
      isSubmitToLhdn: [true],
      returnAgainstId: [null as string | null],
      updateBilledAmountInPurchaseOrder: [true],
      updateStock: [false],
      warehouseId: [''],
      items: fb.array([]),
    });
  }

  function addItem(items: FormArray, opts?: Partial<{ itemId: string; description: string; quantity: number; unitPrice: number; taxAmount: number; uom: string }>) {
    items.push(fb.group({
      itemId: [opts?.itemId ?? '', Validators.required],
      description: [opts?.description ?? '', Validators.required],
      quantity: [opts?.quantity ?? 1, Validators.required],
      unitPrice: [opts?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [opts?.taxAmount ?? 0],
      uom: [opts?.uom ?? 'EA'],
    }));
  }

  function attachEInvoiceSync(form: ReturnType<typeof createPIForm>) {
    form.get('isReturn')?.valueChanges.subscribe((isRet) => {
      const current = form.get('eInvoiceDocType')?.value;
      if (isRet) {
        if (current !== 12 && current !== 14) {
          form.patchValue({ eInvoiceDocType: 12, isDebitNote: false });
        }
      } else {
        if (current !== 11 && current !== 13) {
          form.patchValue({ eInvoiceDocType: 11, isReturnRefund: false });
        }
      }
    });

    form.get('eInvoiceDocType')?.valueChanges.subscribe((docType) => {
      const numType = docType != null ? Number(docType) : null;
      const isRet = numType === 12 || numType === 14;
      const isDebit = numType === 13;
      const isRefund = numType === 14;
      const patch: Record<string, unknown> = {};
      if (form.get('isReturn')?.value !== isRet) {
        patch['isReturn'] = isRet;
      }
      if (form.get('isDebitNote')?.value !== isDebit) {
        patch['isDebitNote'] = isDebit;
      }
      if (form.get('isReturnRefund')?.value !== isRefund) {
        patch['isReturnRefund'] = isRefund;
      }
      if (isDebit && form.get('updateStock')?.value) {
        patch['updateStock'] = false;
      }
      if (Object.keys(patch).length > 0) {
        form.patchValue(patch);
      }
    });
    return form;
  }

  function mapDtoFromForm(form: ReturnType<typeof createPIForm>): any {
    const raw = form.getRawValue() as any;
    return {
      ...raw,
      eInvoiceDocType: raw.eInvoiceDocType != null ? Number(raw.eInvoiceDocType) : undefined,
      isDebitNote: raw.eInvoiceDocType != null ? Number(raw.eInvoiceDocType) === 13 : (raw.isDebitNote ?? false),
      isReturnRefund: raw.eInvoiceDocType != null ? Number(raw.eInvoiceDocType) === 14 : (raw.isReturnRefund ?? false),
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        taxAmount: item.taxAmount ?? 0,
        uom: item.uom ?? 'Unit',
      })),
    };
  }

  describe('form validation', () => {
    it('should require companyId', () => {
      const form = createPIForm();
      form.patchValue({ companyId: '' });
      expect(form.get('companyId')?.valid).toBe(false);
    });

    it('should require supplierId', () => {
      const form = createPIForm();
      expect(form.get('supplierId')?.valid).toBe(false);
    });

    it('should require issueDate', () => {
      const form = createPIForm();
      form.patchValue({ issueDate: '' });
      expect(form.get('issueDate')?.valid).toBe(false);
    });

    it('should default currency to MYR', () => {
      const form = createPIForm();
      expect(form.get('currencyCode')?.value).toBe('MYR');
    });

    it('should default isReturn to false', () => {
      const form = createPIForm();
      expect(form.get('isReturn')?.value).toBe(false);
    });

    it('should default updateStock to false', () => {
      const form = createPIForm();
      expect(form.get('updateStock')?.value).toBe(false);
    });
  });

  describe('item management', () => {
    it('should add item to form array', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', description: 'Widget', quantity: 10, unitPrice: 50 });
      expect(items.length).toBe(1);
      expect(items.at(0).get('quantity')?.value).toBe(10);
    });

    it('should reject empty itemId', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: '' });
      expect(items.at(0).get('itemId')?.valid).toBe(false);
    });

    it('should reject negative unitPrice', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', unitPrice: -10 });
      expect(items.at(0).get('unitPrice')?.valid).toBe(false);
    });

    it('should allow multiple items', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', quantity: 5, unitPrice: 100 });
      addItem(items, { itemId: 'item-2', quantity: 3, unitPrice: 200 });
      expect(items.length).toBe(2);
    });
  });

  describe('DTO mapping', () => {
    it('should map quantity field correctly', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', quantity: 10, unitPrice: 50 });
      const dto = mapDtoFromForm(form);
      expect(dto.items[0].quantity).toBe(10);
      expect(dto.items[0].unitPrice).toBe(50);
    });

    it('should fallback qty to quantity for grid-added items', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      // Simulate grid-added item with qty/rate field names
      items.push(fb.group({ itemId: ['item-1'], qty: [7], rate: [30], itemName: ['Test'], taxAmount: [0], uom: ['EA'] }));
      const raw = form.getRawValue() as any;
      const mapped = (raw.items ?? []).map((item: any) => ({
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        description: item.description || item.itemName || '',
      }));
      expect(mapped[0].quantity).toBe(7);
      expect(mapped[0].unitPrice).toBe(30);
      expect(mapped[0].description).toBe('Test');
    });

    it('should include updateStock and warehouseId', () => {
      const form = createPIForm();
      form.patchValue({ updateStock: true, warehouseId: 'wh-1' });
      const dto = mapDtoFromForm(form);
      expect(dto.updateStock).toBe(true);
      expect(dto.warehouseId).toBe('wh-1');
    });

    it('should preserve isReturn and returnAgainstId', () => {
      const form = createPIForm();
      form.patchValue({ isReturn: true, returnAgainstId: 'pi-orig-001' });
      const dto = mapDtoFromForm(form);
      expect(dto.isReturn).toBe(true);
      expect(dto.returnAgainstId).toBe('pi-orig-001');
    });

    it('should preserve updateBilledAmountInPurchaseOrder for returns', () => {
      const form = createPIForm();
      form.patchValue({ isReturn: true, returnAgainstId: 'pi-orig-001', updateBilledAmountInPurchaseOrder: false });
      const dto = mapDtoFromForm(form);
      expect(dto.updateBilledAmountInPurchaseOrder).toBe(false);
    });

    it('should default uom to Unit when missing', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      items.push(fb.group({ itemId: ['item-1'], quantity: [1], unitPrice: [100], taxAmount: [0], description: ['X'] }));
      const dto = mapDtoFromForm(form);
      expect(dto.items[0].uom).toBe('Unit');
    });
  });

  describe('debit note (return) flow', () => {
    it('should allow negative quantities for returns', () => {
      const form = createPIForm();
      form.patchValue({ isReturn: true, returnAgainstId: 'pi-001' });
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', quantity: -5, unitPrice: 100 });
      // Negative qty is valid for returns
      expect(items.at(0).get('quantity')?.value).toBe(-5);
    });

    it('should set notes for debit note', () => {
      const form = createPIForm();
      form.patchValue({
        isReturn: true,
        returnAgainstId: 'pi-001',
        notes: 'Debit Note against PI-2026-00005',
      });
      expect(form.get('notes')?.value).toContain('Debit Note');
    });

    it('should default isSubmitToLhdn to true and map toggled false to DTO', () => {
      const form = createPIForm();
      expect(form.get('isSubmitToLhdn')?.value).toBe(true);
      form.patchValue({ isSubmitToLhdn: false });
      expect(mapDtoFromForm(form).isSubmitToLhdn).toBe(false);
    });

    it('should sync eInvoiceDocType (11/12/13/14) bidirectionally with isReturn and isReturnRefund', () => {
      const form = attachEInvoiceSync(createPIForm());
      expect(form.get('eInvoiceDocType')?.value).toBe(11);

      form.patchValue({ isReturn: true });
      expect(form.get('eInvoiceDocType')?.value).toBe(12);

      form.patchValue({ eInvoiceDocType: 14 });
      const refundDto = mapDtoFromForm(form);
      expect(refundDto.isReturn).toBe(true);
      expect(refundDto.isReturnRefund).toBe(true);
      expect(refundDto.eInvoiceDocType).toBe(14);

      form.patchValue({ updateStock: true, eInvoiceDocType: 13 });
      const dnDto = mapDtoFromForm(form);
      expect(dnDto.isReturn).toBe(false);
      expect(dnDto.isDebitNote).toBe(true);
      expect(dnDto.updateStock).toBe(false);
      expect(dnDto.isReturnRefund).toBe(false);
      expect(dnDto.eInvoiceDocType).toBe(13);
    });

    it('does not apply default tax template when creating a return against a purchase invoice without taxes (PR #66918)', () => {
      const piServiceMock = {
        get: vi.fn().mockReturnValue(of({
          id: 'pi-orig',
          invoiceNumber: 'PINV-0010',
          companyId: 'comp-1',
          supplierId: 'sup-1',
          currencyCode: 'MYR',
          taxAmount: 0,
          items: [{ itemId: 'item-1', description: 'Zero-tax goods', quantity: 5, unitPrice: 20, taxAmount: 0, uom: 'EA' }],
        })),
        checkDuplicateSupplierInvoice: vi.fn().mockReturnValue(of({ isDuplicate: false })),
      };
      const taxTemplateMock = {
        getActiveTemplates: vi.fn().mockReturnValue(of([{ id: 'tmpl-b1', isDefault: true, rows: [{ rate: 6, description: 'Input SST 6%' }] }])),
        getDefault: vi.fn().mockReturnValue(of({ id: 'tmpl-b1', isDefault: true, rows: [{ rate: 6, description: 'Input SST 6%' }] })),
      };
      const partyDetailsMock = {
        getSupplierDetails: vi.fn().mockReturnValue(of({ taxCategoryId: 'tc-b1' })),
      };
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParams: { returnAgainst: 'pi-orig' } } } },
          { provide: TaxCalculationService, useValue: new TaxCalculationService() },
          { provide: PurchaseInvoiceService, useValue: piServiceMock },
          { provide: SupplierService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
          { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: PaymentTermsTemplateService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warn: vi.fn(), info: vi.fn() } },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: PartyDetailsService, useValue: partyDetailsMock },
          { provide: TaxCategoryService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxRuleService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxChargesTemplateService, useValue: taxTemplateMock },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new PurchaseInvoiceFormComponent());
      comp.ngOnInit();

      expect(taxTemplateMock.getDefault).not.toHaveBeenCalled();
      expect(comp.selectedTaxRules()).toEqual([]);
      expect(comp.calcResult.totalTax).toBe(0);
    });
  });
});
