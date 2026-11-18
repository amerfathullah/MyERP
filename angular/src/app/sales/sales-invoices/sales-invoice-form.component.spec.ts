import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormArray, Validators } from '@angular/forms';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { TaxCalculationService } from '../../shared/services/tax-calculation.service';
import { SalesInvoiceFormComponent } from './sales-invoice-form.component';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SalesPersonService } from '../../proxy/sales/sales-person.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { PaymentTermsTemplateService } from '../../proxy/accounting/payment-terms-template.service';
import { CurrencyExchangeService } from '../../proxy/accounting/currency-exchange.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { TaxChargesTemplateService } from '../../proxy/tax/tax-charges-template.service';
import { PriceListService } from '../../proxy/inventory/price-list.service';
import { TaxTemplateType } from '../../proxy/tax/tax-template-type.enum';

/**
 * Tests for the Sales Invoice form calculation patterns.
 * Tests the recalculate flow and item management without full Angular TestBed.
 */
describe('SalesInvoice form calculations', () => {
  const fb = new FormBuilder();
  const taxCalc = new TaxCalculationService();

  function createItemsFormArray(): FormArray {
    return fb.array([]);
  }

  function addItemRow(items: FormArray, item?: { quantity?: number; unitPrice?: number; description?: string }) {
    items.push(fb.group({
      itemId: [item?.description ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [0],
      uom: ['EA'],
    }));
  }

  function recalculate(items: FormArray) {
    const itemValues = items.controls.map(c => ({
      qty: c.get('quantity')?.value ?? 0,
      rate: c.get('unitPrice')?.value ?? 0,
      discountPercent: 0,
    }));
    return taxCalc.calculate(itemValues, []);
  }

  describe('addItemRow', () => {
    it('should add item with defaults', () => {
      const items = createItemsFormArray();
      addItemRow(items);
      expect(items.length).toBe(1);
      expect(items.at(0).get('quantity')?.value).toBe(1);
      expect(items.at(0).get('unitPrice')?.value).toBe(0);
    });

    it('should add item with specified values', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 5, unitPrice: 100, description: 'Widget' });
      expect(items.at(0).get('quantity')?.value).toBe(5);
      expect(items.at(0).get('unitPrice')?.value).toBe(100);
      expect(items.at(0).get('description')?.value).toBe('Widget');
    });

    it('should support multiple rows', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 2, unitPrice: 50 });
      addItemRow(items, { quantity: 3, unitPrice: 75 });
      expect(items.length).toBe(2);
    });

    it('should support negative quantities for returns', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: -3, unitPrice: 100 });
      expect(items.at(0).get('quantity')?.value).toBe(-3);
    });
  });

  describe('recalculate', () => {
    it('should calculate correct netTotal for single item', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 10, unitPrice: 50 });
      const result = recalculate(items);
      expect(result.netTotal).toBe(500);
      expect(result.grandTotal).toBe(500); // No tax
    });

    it('should calculate correct netTotal for multiple items', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 2, unitPrice: 100 });
      addItemRow(items, { quantity: 5, unitPrice: 30 });
      const result = recalculate(items);
      expect(result.netTotal).toBe(350); // 200 + 150
      expect(result.grandTotal).toBe(350);
    });

    it('should handle empty items array', () => {
      const items = createItemsFormArray();
      const result = recalculate(items);
      expect(result.netTotal).toBe(0);
      expect(result.grandTotal).toBe(0);
    });

    it('should handle zero quantity', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 0, unitPrice: 100 });
      const result = recalculate(items);
      expect(result.netTotal).toBe(0);
    });

    it('should handle negative quantities (credit notes)', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: -3, unitPrice: 100 });
      const result = recalculate(items);
      expect(result.netTotal).toBe(-300);
      expect(result.grandTotal).toBe(-300);
    });

    it('should recalculate after item modification', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 5, unitPrice: 100 });

      let result = recalculate(items);
      expect(result.netTotal).toBe(500);

      // User changes quantity
      items.at(0).get('quantity')?.setValue(10);
      result = recalculate(items);
      expect(result.netTotal).toBe(1000);
    });

    it('should recalculate after item removal', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 2, unitPrice: 100 });
      addItemRow(items, { quantity: 3, unitPrice: 50 });

      expect(recalculate(items).netTotal).toBe(350);

      items.removeAt(1);
      expect(recalculate(items).netTotal).toBe(200);
    });
  });

  describe('form validation', () => {
    it('should require customerId', () => {
      const form = fb.group({
        companyId: ['comp-1', Validators.required],
        customerId: ['', Validators.required],
        issueDate: ['2026-01-01', Validators.required],
        items: fb.array([]),
      });
      expect(form.valid).toBe(false);
      form.get('customerId')?.setValue('cust-1');
      expect(form.valid).toBe(true);
    });

    it('should require companyId', () => {
      const form = fb.group({
        companyId: ['', Validators.required],
        customerId: ['cust-1', Validators.required],
        issueDate: ['2026-01-01', Validators.required],
      });
      expect(form.valid).toBe(false);
    });

    it('should validate item unitPrice min 0', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 1, unitPrice: -5 });
      const row = items.at(0);
      expect(row.get('unitPrice')?.valid).toBe(false);
    });

    it('should validate item quantity required', () => {
      const items = createItemsFormArray();
      items.push(fb.group({
        quantity: [null, [Validators.required]],
        unitPrice: [100],
      }));
      expect(items.at(0).get('quantity')?.valid).toBe(false);
    });
  });

  describe('with tax calculation', () => {
    it('should calculate SST 6% on invoice total', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 10, unitPrice: 100 });

      const itemValues = items.controls.map(c => ({
        qty: c.get('quantity')?.value ?? 0,
        rate: c.get('unitPrice')?.value ?? 0,
        discountPercent: 0,
      }));

      const result = taxCalc.calculate(itemValues, [
        { taxName: 'SST', rate: 6, chargeType: 'OnNetTotal' }
      ]);

      expect(result.netTotal).toBe(1000);
      expect(result.totalTax).toBe(60);
      expect(result.grandTotal).toBe(1060);
    });

    it('should handle multi-item with SST', () => {
      const items = createItemsFormArray();
      addItemRow(items, { quantity: 5, unitPrice: 200 });
      addItemRow(items, { quantity: 2, unitPrice: 50 });

      const itemValues = items.controls.map(c => ({
        qty: c.get('quantity')?.value ?? 0,
        rate: c.get('unitPrice')?.value ?? 0,
        discountPercent: 0,
      }));

      const result = taxCalc.calculate(itemValues, [
        { taxName: 'SST', rate: 6, chargeType: 'OnNetTotal' }
      ]);

      expect(result.netTotal).toBe(1100); // 1000 + 100
      expect(result.totalTax).toBe(66); // 6% of 1100
      expect(result.grandTotal).toBe(1166);
    });
  });

  describe('LHDN submission flag', () => {
    it('should default isSubmitToLhdn to true and allow opt-out', () => {
      const form = fb.group({ isSubmitToLhdn: [true] });
      expect(form.get('isSubmitToLhdn')?.value).toBe(true);
      form.patchValue({ isSubmitToLhdn: false });
      expect(form.getRawValue().isSubmitToLhdn).toBe(false);
    });

    it('should sync eInvoiceDocType (01/02/03/04) with isReturn, isDebitNote, isReturnRefund, and updateStock', () => {
      const form = fb.group({
        isReturn: [false],
        isDebitNote: [false],
        isReturnRefund: [false],
        eInvoiceDocType: [1 as number | null],
        updateStock: [true],
      });

      form.get('eInvoiceDocType')?.valueChanges.subscribe((docType) => {
        const numType = docType != null ? Number(docType) : null;
        const isRet = numType === 2 || numType === 4;
        const isDebit = numType === 3;
        const isRefund = numType === 4;
        const patch: Record<string, unknown> = {};
        if (form.get('isReturn')?.value !== isRet) patch['isReturn'] = isRet;
        if (form.get('isDebitNote')?.value !== isDebit) patch['isDebitNote'] = isDebit;
        if (form.get('isReturnRefund')?.value !== isRefund) patch['isReturnRefund'] = isRefund;
        if (isDebit && form.get('updateStock')?.value) patch['updateStock'] = false;
        if (Object.keys(patch).length > 0) form.patchValue(patch);
      });

      form.patchValue({ eInvoiceDocType: 4 });
      expect(form.get('isReturn')?.value).toBe(true);
      expect(form.get('isReturnRefund')?.value).toBe(true);
      expect(form.get('isDebitNote')?.value).toBe(false);

      form.patchValue({ eInvoiceDocType: 3 });
      expect(form.get('isReturn')?.value).toBe(false);
      expect(form.get('isReturnRefund')?.value).toBe(false);
      expect(form.get('isDebitNote')?.value).toBe(true);
      expect(form.get('updateStock')?.value).toBe(false);
    });
  });

  describe('Tax template auto-loading', () => {
    it('loads active selling templates and applies default tax template for company', () => {
      const tmpl = {
        id: 'tmpl-1',
        name: 'SST 8%',
        isDefault: true,
        rows: [{ id: 'row-1', rate: 8, description: 'Service Tax 8%', chargeType: 0 }],
      };
      const taxTemplateMock = {
        getActiveTemplates: vi.fn().mockReturnValue(of([tmpl])),
        getDefault: vi.fn().mockReturnValue(of(tmpl)),
      };
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParams: {} } } },
          { provide: TaxCalculationService, useValue: new TaxCalculationService() },
          { provide: SalesInvoiceService, useValue: { get: vi.fn(), getNextNumber: vi.fn().mockReturnValue(of('SINV-0001')) } },
          { provide: SalesOrderService, useValue: { getBillableSalesOrders: vi.fn().mockReturnValue(of([])) } },
          { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: SalesPersonService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
          { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: PaymentTermsTemplateService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: PartyDetailsService, useValue: {} },
          { provide: TaxCategoryService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxRuleService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxChargesTemplateService, useValue: taxTemplateMock },
          { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warn: vi.fn(), info: vi.fn() } },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new SalesInvoiceFormComponent());
      comp.ngOnInit();

      expect(taxTemplateMock.getActiveTemplates).toHaveBeenCalledWith('comp-1', TaxTemplateType.Selling);
      expect(taxTemplateMock.getDefault).toHaveBeenCalledWith('comp-1', TaxTemplateType.Selling);
      expect(comp.taxTemplates()).toHaveLength(1);
      expect(comp.selectedTaxRules()).toHaveLength(1);
      expect(comp.selectedTaxRules()[0].rate).toBe(8);
    });

    it('loads billable sales orders picker when multiple billable orders exist and filters unbilled items to selected order', () => {
      const siServiceMock = {
        get: vi.fn(),
        getNextNumber: vi.fn().mockReturnValue(of('SINV-0001')),
        getUnbilledOrderItems: vi.fn().mockReturnValue(of([
          { salesOrderId: 'so-1', salesOrderItemId: 'soi-1', itemId: 'item-1', itemName: 'Widget A', quantity: 2, rate: 50, uom: 'EA' },
          { salesOrderId: 'so-2', salesOrderItemId: 'soi-2', itemId: 'item-2', itemName: 'Widget B', quantity: 3, rate: 40, uom: 'EA' },
        ])),
      };
      const soServiceMock = {
        getBillableSalesOrders: vi.fn().mockReturnValue(of([
          { id: 'so-1', orderNumber: 'SO-001', grandTotal: 100, perBilled: 0 },
          { id: 'so-2', orderNumber: 'SO-002', grandTotal: 120, perBilled: 0 },
        ])),
      };
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParams: {} } } },
          { provide: TaxCalculationService, useValue: new TaxCalculationService() },
          { provide: SalesInvoiceService, useValue: siServiceMock },
          { provide: SalesOrderService, useValue: soServiceMock },
          { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: SalesPersonService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
          { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: PaymentTermsTemplateService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: PartyDetailsService, useValue: {} },
          { provide: TaxCategoryService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxRuleService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxChargesTemplateService, useValue: { getActiveTemplates: vi.fn().mockReturnValue(of([])), getDefault: vi.fn().mockReturnValue(of(null)) } },
          { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warn: vi.fn(), info: vi.fn() } },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new SalesInvoiceFormComponent());
      comp.form.patchValue({ customerId: 'cust-1', companyId: 'comp-1' });
      comp.loadFromSalesOrders();

      expect(soServiceMock.getBillableSalesOrders).toHaveBeenCalledWith('cust-1', 'comp-1');
      expect(comp.showBillableSoPicker()).toBe(true);
      expect(comp.billableSalesOrders()).toHaveLength(2);

      comp.toggleBillableSo('so-2', false);
      comp.loadSelectedBillableOrders();

      expect(siServiceMock.getUnbilledOrderItems).toHaveBeenCalledWith('cust-1', 'comp-1');
      expect(comp.items.length).toBe(1);
      expect(comp.items.at(0).get('salesOrderItemId')?.value).toBe('soi-1');
      expect(comp.showBillableSoPicker()).toBe(false);
    });

    it('does not apply default tax template when creating a return against an invoice without taxes (PR #66918)', () => {
      const siServiceMock = {
        get: vi.fn().mockReturnValue(of({
          id: 'si-orig',
          invoiceNumber: 'SINV-0010',
          companyId: 'comp-1',
          customerId: 'cust-1',
          customerName: 'Acme',
          currencyCode: 'MYR',
          taxAmount: 0,
          items: [{ itemId: 'item-1', description: 'Zero-tax item', quantity: 2, unitPrice: 100, taxAmount: 0, uom: 'EA' }],
        })),
        getNextNumber: vi.fn().mockReturnValue(of('SINV-0011')),
      };
      const taxTemplateMock = {
        getActiveTemplates: vi.fn().mockReturnValue(of([{ id: 'tmpl-1', isDefault: true, rows: [{ rate: 8, description: 'SST 8%' }] }])),
        getDefault: vi.fn().mockReturnValue(of({ id: 'tmpl-1', isDefault: true, rows: [{ rate: 8, description: 'SST 8%' }] })),
      };
      const partyDetailsMock = {
        getCustomerDetails: vi.fn().mockReturnValue(of({ taxCategoryId: 'tc-1', outstandingAmount: 0, creditLimit: 0 })),
      };
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParams: { returnAgainst: 'si-orig' } } } },
          { provide: TaxCalculationService, useValue: new TaxCalculationService() },
          { provide: SalesInvoiceService, useValue: siServiceMock },
          { provide: SalesOrderService, useValue: { getBillableSalesOrders: vi.fn().mockReturnValue(of([])) } },
          { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: SalesPersonService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
          { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: PaymentTermsTemplateService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: PartyDetailsService, useValue: partyDetailsMock },
          { provide: TaxCategoryService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxRuleService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxChargesTemplateService, useValue: taxTemplateMock },
          { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warn: vi.fn(), info: vi.fn() } },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new SalesInvoiceFormComponent());
      comp.ngOnInit();

      expect(taxTemplateMock.getDefault).not.toHaveBeenCalled();
      expect(comp.selectedTaxRules()).toEqual([]);
      expect(comp.calcResult.totalTax).toBe(0);
    });

    it('duplicateFrom clears salesOrderItemId and deliveryNoteItemId on cloned rows', () => {
      const siServiceMock = {
        get: vi.fn().mockReturnValue(of({
          id: 'si-orig',
          invoiceNumber: 'SINV-0010',
          companyId: 'comp-1',
          customerId: 'cust-1',
          customerName: 'Acme',
          currencyCode: 'MYR',
          items: [{
            itemId: 'item-1',
            description: 'Widget',
            quantity: 2,
            unitPrice: 100,
            taxAmount: 0,
            uom: 'EA',
            salesOrderItemId: 'soi-1',
            deliveryNoteItemId: 'dni-1',
          }],
        })),
        getNextNumber: vi.fn().mockReturnValue(of('SINV-0012')),
      };
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParams: { duplicateFrom: 'si-orig' } } } },
          { provide: TaxCalculationService, useValue: new TaxCalculationService() },
          { provide: SalesInvoiceService, useValue: siServiceMock },
          { provide: SalesOrderService, useValue: { getBillableSalesOrders: vi.fn().mockReturnValue(of([])) } },
          { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: SalesPersonService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
          { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: PaymentTermsTemplateService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CurrencyExchangeService, useValue: {} },
          { provide: PartyDetailsService, useValue: { getCustomerDetails: vi.fn().mockReturnValue(of({})) } },
          { provide: TaxCategoryService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxRuleService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: TaxChargesTemplateService, useValue: { getActiveTemplates: vi.fn().mockReturnValue(of([])), getDefault: vi.fn().mockReturnValue(of(null)) } },
          { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warn: vi.fn(), info: vi.fn() } },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new SalesInvoiceFormComponent());
      comp.ngOnInit();

      expect(comp.items.length).toBe(1);
      expect(comp.items.at(0).get('salesOrderItemId')?.value).toBeNull();
      expect(comp.items.at(0).get('deliveryNoteItemId')?.value).toBeNull();
    });
  });
});
