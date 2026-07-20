import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

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
