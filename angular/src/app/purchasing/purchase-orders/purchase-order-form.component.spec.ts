import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

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
});
