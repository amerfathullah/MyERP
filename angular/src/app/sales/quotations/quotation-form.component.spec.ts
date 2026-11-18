import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { QuotationFormComponent } from './quotation-form.component';
import { TaxCalculationService } from '../../shared/services/tax-calculation.service';
import { QuotationService } from '../../proxy/sales/quotation.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PriceListService } from '../../proxy/inventory/price-list.service';
import { PartyDetailsService } from '../../proxy/core/party-details.service';

/**
 * Tests for the Quotation form DTO mapping.
 * Quotation uses the same invoice-item-grid as SI/SO — verifies the same mapping bugs don't recur.
 */
describe('Quotation form DTO mapping', () => {
  const fb = new FormBuilder();

  function createQuotationForm() {
    return fb.group({
      companyId: ['comp-1', Validators.required],
      customerId: ['', Validators.required],
      issueDate: ['2026-07-20', Validators.required],
      validUntil: [''],
      notes: [''],
      items: fb.array([]),
    });
  }

  function addItem(items: FormArray, opts?: { itemId?: string; qty?: number; rate?: number; itemName?: string }) {
    items.push(fb.group({
      itemId: [opts?.itemId ?? '', Validators.required],
      itemName: [opts?.itemName ?? ''],
      qty: [opts?.qty ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [opts?.rate ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [0],
    }));
  }

  function mapFormToDto(form: ReturnType<typeof createQuotationForm>): any {
    const raw = form.getRawValue();
    return {
      companyId: raw.companyId,
      customerId: raw.customerId,
      issueDate: raw.issueDate,
      validUntil: raw.validUntil || undefined,
      notes: raw.notes,
      items: (raw.items as any[]).map(item => ({
        itemId: item.itemId,
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        description: item.description || item.itemName || '',
        taxAmount: item.taxAmount ?? 0,
      })),
    };
  }

  it('should map qty→quantity from grid', () => {
    const form = createQuotationForm();
    form.patchValue({ customerId: 'cust-1' });
    addItem(form.get('items') as FormArray, { itemId: 'item-1', qty: 8, rate: 250 });
    const dto = mapFormToDto(form);
    expect(dto.items[0].quantity).toBe(8);
    expect(dto.items[0].unitPrice).toBe(250);
  });

  it('should map itemName→description', () => {
    const form = createQuotationForm();
    form.patchValue({ customerId: 'cust-1' });
    addItem(form.get('items') as FormArray, { itemId: 'x', qty: 1, rate: 50, itemName: 'Service Package' });
    const dto = mapFormToDto(form);
    expect(dto.items[0].description).toBe('Service Package');
  });

  it('should exclude empty validUntil', () => {
    const form = createQuotationForm();
    form.patchValue({ customerId: 'cust-1', validUntil: '' });
    addItem(form.get('items') as FormArray, { itemId: 'x', qty: 1, rate: 100 });
    const dto = mapFormToDto(form);
    expect(dto.validUntil).toBeUndefined();
  });

  it('should include validUntil when set', () => {
    const form = createQuotationForm();
    form.patchValue({ customerId: 'cust-1', validUntil: '2026-08-20' });
    addItem(form.get('items') as FormArray, { itemId: 'x', qty: 1, rate: 100 });
    const dto = mapFormToDto(form);
    expect(dto.validUntil).toBe('2026-08-20');
  });

  it('should handle zero-qty items (rate-only quoting)', () => {
    const form = createQuotationForm();
    form.patchValue({ customerId: 'cust-1' });
    const items = form.get('items') as FormArray;
    items.push(fb.group({ itemId: ['x'], qty: [0], rate: [500], itemName: ['Consulting'], taxAmount: [0] }));
    const dto = mapFormToDto(form);
    // Quotation allows zero qty (rate-only) per Selling Settings allow_zero_qty_in_quotation
    expect(dto.items[0].quantity).toBe(0);
    expect(dto.items[0].unitPrice).toBe(500);
  });

  it('should calculate net total from items', () => {
    const items = [
      { qty: 2, rate: 1000 },
      { qty: 5, rate: 200 },
      { qty: 1, rate: 500 },
    ];
    const netTotal = items.reduce((sum, i) => sum + (i.qty * i.rate), 0);
    expect(netTotal).toBe(3500); // 2000 + 1000 + 500
  });
});

describe('Quotation expiry logic', () => {
  function isExpired(validUntil: string | null | undefined): boolean {
    if (!validUntil) return false;
    return new Date(validUntil) < new Date(new Date().toISOString().slice(0, 10));
  }

  it('should not be expired when no validUntil set', () => {
    expect(isExpired(null)).toBe(false);
    expect(isExpired(undefined)).toBe(false);
    expect(isExpired('')).toBe(false);
  });

  it('should be expired when past date', () => {
    expect(isExpired('2020-01-01')).toBe(true);
  });

  it('should not be expired for future date', () => {
    expect(isExpired('2099-12-31')).toBe(false);
  });
});

describe('QuotationFormComponent Prospect Details', () => {
  it('prefills prospect name and price list from PartyDetailsService.getProspectDetails', () => {
    const partyMock = {
      getProspectDetails: vi.fn().mockReturnValue(of({ partyName: 'Prospect Corp', priceListId: 'pl-retail' })),
      getCustomerDetails: vi.fn().mockReturnValue(of({})),
    };
    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParamMap: { get: (k: string) => (k === 'prospectId' ? 'pros-1' : null) } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: TaxCalculationService, useValue: new TaxCalculationService() },
        { provide: QuotationService, useValue: {} },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyDetailsService, useValue: partyMock },
        { provide: ToasterService, useValue: { error: vi.fn(), success: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QuotationFormComponent());
    comp.ngOnInit();

    expect(partyMock.getProspectDetails).toHaveBeenCalledWith({ partyId: 'pros-1', companyId: 'comp-1' });
    expect(comp.form.get('customerName')?.value).toBe('Prospect Corp');
    expect(comp.form.get('priceListId')?.value).toBe('pl-retail');
  });

  it('shows error toast when save fails', () => {
    const toasterMock = { error: vi.fn(), success: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: TaxCalculationService, useValue: new TaxCalculationService() },
        { provide: QuotationService, useValue: { create: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Quotation rejected' } } }))) } },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyDetailsService, useValue: { getCustomerDetails: vi.fn().mockReturnValue(of({})) } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QuotationFormComponent());
    comp.form.patchValue({ companyId: 'comp-1', customerId: 'cust-1', quotationDate: new Date() });
    comp.save();

    expect(toasterMock.error).toHaveBeenCalledWith('Quotation rejected');
  });

  it('preserves blanketOrderId on item rows when saving', () => {
    const createSpy = vi.fn().mockReturnValue(of({ id: 'q-1' }));
    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: TaxCalculationService, useValue: new TaxCalculationService() },
        { provide: QuotationService, useValue: { create: createSpy } },
        { provide: CustomerService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        { provide: PriceListService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: PartyDetailsService, useValue: { getCustomerDetails: vi.fn().mockReturnValue(of({})) } },
        { provide: ToasterService, useValue: { error: vi.fn(), success: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new QuotationFormComponent());
    const fbLocal = new FormBuilder();
    comp.items.push(fbLocal.group({
      itemId: ['item-1'],
      itemName: ['Widget'],
      qty: [5],
      rate: [120],
      discountPercent: [0],
      blanketOrderId: ['bo-123'],
    }));
    comp.form.patchValue({ companyId: 'comp-1', customerId: 'cust-1', quotationDate: new Date() });
    comp.save();

    expect(createSpy).toHaveBeenCalledTimes(1);
    expect(createSpy.mock.calls[0][0].items[0].blanketOrderId).toBe('bo-123');
  });
});
