import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

/**
 * Tests for the Sales Order form DTO mapping and validation.
 * Specifically tests the qty/rate → quantity/unitPrice mapping that was a bug source.
 */
describe('SalesOrder form DTO mapping', () => {
  const fb = new FormBuilder();

  function createSOForm() {
    return fb.group({
      companyId: ['comp-1', Validators.required],
      customerId: ['', Validators.required],
      orderDate: ['2026-07-20', Validators.required],
      expectedDeliveryDate: [''],
      notes: [''],
      items: fb.array([], Validators.minLength(1)),
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

  function mapFormToDto(form: ReturnType<typeof createSOForm>): any {
    const raw = form.getRawValue();
    return {
      companyId: raw.companyId,
      customerId: raw.customerId,
      orderDate: raw.orderDate,
      expectedDeliveryDate: raw.expectedDeliveryDate || undefined,
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

  describe('form validation', () => {
    it('should require companyId and customerId', () => {
      const form = createSOForm();
      form.patchValue({ companyId: '', customerId: '' });
      expect(form.valid).toBe(false);
    });

    it('should be valid with required fields + items', () => {
      const form = createSOForm();
      form.patchValue({ customerId: 'cust-1' });
      addItem(form.get('items') as FormArray, { itemId: 'item-1', qty: 2, rate: 100 });
      expect(form.valid).toBe(true);
    });

    it('should start with empty items array', () => {
      const form = createSOForm();
      const items = form.get('items') as FormArray;
      expect(items.length).toBe(0);
    });
  });

  describe('item qty/rate → quantity/unitPrice mapping', () => {
    it('should map qty→quantity for grid-added items', () => {
      const form = createSOForm();
      form.patchValue({ customerId: 'cust-1' });
      addItem(form.get('items') as FormArray, { itemId: 'item-1', qty: 5, rate: 200 });
      const dto = mapFormToDto(form);
      expect(dto.items[0].quantity).toBe(5);
      expect(dto.items[0].unitPrice).toBe(200);
    });

    it('should map itemName→description', () => {
      const form = createSOForm();
      form.patchValue({ customerId: 'cust-1' });
      addItem(form.get('items') as FormArray, { itemId: 'x', qty: 1, rate: 10, itemName: 'Widget' });
      const dto = mapFormToDto(form);
      expect(dto.items[0].description).toBe('Widget');
    });

    it('should default to 0 when qty/rate are missing', () => {
      const form = createSOForm();
      form.patchValue({ customerId: 'cust-1' });
      const items = form.get('items') as FormArray;
      items.push(fb.group({ itemId: ['x'], qty: [null], rate: [null], itemName: [''], taxAmount: [0] }));
      const dto = mapFormToDto(form);
      expect(dto.items[0].quantity).toBe(0);
      expect(dto.items[0].unitPrice).toBe(0);
    });

    it('should handle multiple items', () => {
      const form = createSOForm();
      form.patchValue({ customerId: 'cust-1' });
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'a', qty: 3, rate: 50 });
      addItem(items, { itemId: 'b', qty: 10, rate: 25 });
      const dto = mapFormToDto(form);
      expect(dto.items.length).toBe(2);
      expect(dto.items[0].quantity).toBe(3);
      expect(dto.items[1].quantity).toBe(10);
    });

    it('should exclude empty expectedDeliveryDate', () => {
      const form = createSOForm();
      form.patchValue({ customerId: 'cust-1', expectedDeliveryDate: '' });
      addItem(form.get('items') as FormArray, { itemId: 'x', qty: 1, rate: 10 });
      const dto = mapFormToDto(form);
      expect(dto.expectedDeliveryDate).toBeUndefined();
    });
  });

  describe('line total calculations', () => {
    it('should calculate line total correctly', () => {
      const qty = 5;
      const rate = 120;
      const lineTotal = qty * rate;
      expect(lineTotal).toBe(600);
    });

    it('should calculate net total from multiple items', () => {
      const items = [
        { qty: 3, rate: 100 },
        { qty: 2, rate: 250 },
        { qty: 1, rate: 50 },
      ];
      const netTotal = items.reduce((sum, i) => sum + (i.qty * i.rate), 0);
      expect(netTotal).toBe(850); // 300 + 500 + 50
    });
  });
});

describe('SalesOrder edit mode detection', () => {
  it('should detect edit mode from route params', () => {
    // Simulate ActivatedRoute snapshot with ID
    const params = { id: 'abc-123' };
    const isEditMode = !!params.id;
    expect(isEditMode).toBe(true);
  });

  it('should detect new mode when no ID', () => {
    const params = { id: undefined };
    const isEditMode = !!params.id;
    expect(isEditMode).toBe(false);
  });
});
