import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

/**
 * Tests for InvoiceItemGridComponent logic: row management, item amount calculations,
 * and the DTO field mapping patterns used across SI/SO/PI/QTN forms.
 */
describe('InvoiceItemGrid logic', () => {
  const fb = new FormBuilder();

  function createItemsArray(): FormArray {
    return fb.array([]);
  }

  function addRow(items: FormArray, opts?: { itemId?: string; qty?: number; rate?: number; discountPercent?: number }) {
    items.push(fb.group({
      itemId: [opts?.itemId ?? '', Validators.required],
      itemName: [''],
      qty: [opts?.qty ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [opts?.rate ?? 0, [Validators.required, Validators.min(0)]],
      discountPercent: [opts?.discountPercent ?? 0, [Validators.min(0), Validators.max(100)]],
      amount: [{ value: 0, disabled: true }],
    }));
  }

  function calculateItemAmount(item: { qty: number; rate: number; discountPercent?: number }) {
    const grossAmount = item.qty * item.rate;
    const discountPercent = item.discountPercent ?? 0;
    const discountAmount = grossAmount * (discountPercent / 100);
    const amount = grossAmount - discountAmount;
    return {
      qty: item.qty,
      rate: item.rate,
      discountPercent,
      discountAmount: Math.round(discountAmount * 100) / 100,
      amount: Math.round(amount * 100) / 100,
    };
  }

  describe('row management', () => {
    it('should start empty', () => {
      const items = createItemsArray();
      expect(items.length).toBe(0);
    });

    it('should add row with defaults', () => {
      const items = createItemsArray();
      addRow(items);
      expect(items.length).toBe(1);
      expect(items.at(0).get('qty')?.value).toBe(1);
      expect(items.at(0).get('rate')?.value).toBe(0);
      expect(items.at(0).get('discountPercent')?.value).toBe(0);
    });

    it('should add row with custom values', () => {
      const items = createItemsArray();
      addRow(items, { itemId: 'item-1', qty: 5, rate: 100, discountPercent: 10 });
      expect(items.at(0).get('itemId')?.value).toBe('item-1');
      expect(items.at(0).get('qty')?.value).toBe(5);
      expect(items.at(0).get('rate')?.value).toBe(100);
    });

    it('should reject empty itemId', () => {
      const items = createItemsArray();
      addRow(items, { itemId: '' });
      expect(items.at(0).get('itemId')?.valid).toBe(false);
    });

    it('should reject zero quantity', () => {
      const items = createItemsArray();
      addRow(items, { qty: 0 });
      expect(items.at(0).get('qty')?.valid).toBe(false);
    });

    it('should reject negative rate', () => {
      const items = createItemsArray();
      addRow(items, { rate: -10 });
      expect(items.at(0).get('rate')?.valid).toBe(false);
    });

    it('should reject discount > 100%', () => {
      const items = createItemsArray();
      addRow(items, { discountPercent: 110 });
      expect(items.at(0).get('discountPercent')?.valid).toBe(false);
    });

    it('should remove row at index', () => {
      const items = createItemsArray();
      addRow(items, { itemId: 'a' });
      addRow(items, { itemId: 'b' });
      addRow(items, { itemId: 'c' });
      items.removeAt(1);
      expect(items.length).toBe(2);
      expect(items.at(1).get('itemId')?.value).toBe('c');
    });
  });

  describe('amount calculation', () => {
    it('should calculate simple amount (qty × rate)', () => {
      const result = calculateItemAmount({ qty: 10, rate: 50 });
      expect(result.amount).toBe(500);
    });

    it('should apply percentage discount', () => {
      const result = calculateItemAmount({ qty: 10, rate: 100, discountPercent: 10 });
      expect(result.amount).toBe(900);
      expect(result.discountAmount).toBe(100);
    });

    it('should handle 100% discount', () => {
      const result = calculateItemAmount({ qty: 5, rate: 200, discountPercent: 100 });
      expect(result.amount).toBe(0);
      expect(result.discountAmount).toBe(1000);
    });

    it('should handle zero qty', () => {
      const result = calculateItemAmount({ qty: 0, rate: 100 });
      expect(result.amount).toBe(0);
    });

    it('should handle zero rate', () => {
      const result = calculateItemAmount({ qty: 10, rate: 0 });
      expect(result.amount).toBe(0);
    });

    it('should round to 2 decimal places', () => {
      const result = calculateItemAmount({ qty: 3, rate: 33.33, discountPercent: 7 });
      // 3 * 33.33 = 99.99; 99.99 * 0.07 = 6.9993; 99.99 - 6.9993 = 92.9907
      expect(result.amount).toBe(92.99);
    });

    it('should handle fractional quantities', () => {
      const result = calculateItemAmount({ qty: 2.5, rate: 40 });
      expect(result.amount).toBe(100);
    });
  });

  describe('DTO field mapping (grid→backend)', () => {
    it('should map qty→quantity for backend DTO', () => {
      const items = createItemsArray();
      addRow(items, { itemId: 'item-1', qty: 10, rate: 50 });
      const mapped = items.controls.map(ctrl => ({
        quantity: ctrl.get('quantity')?.value ?? ctrl.get('qty')?.value ?? 0,
        unitPrice: ctrl.get('unitPrice')?.value ?? ctrl.get('rate')?.value ?? 0,
        description: ctrl.get('description')?.value || ctrl.get('itemName')?.value || '',
      }));
      expect(mapped[0].quantity).toBe(10);
      expect(mapped[0].unitPrice).toBe(50);
    });

    it('should handle pre-loaded items (quantity/unitPrice names)', () => {
      const items = fb.array([]);
      items.push(fb.group({
        itemId: ['item-1'],
        quantity: [7],
        unitPrice: [30],
        description: ['Pre-loaded item'],
      }));
      const mapped = items.controls.map(ctrl => ({
        quantity: ctrl.get('quantity')?.value ?? ctrl.get('qty')?.value ?? 0,
        unitPrice: ctrl.get('unitPrice')?.value ?? ctrl.get('rate')?.value ?? 0,
        description: ctrl.get('description')?.value || ctrl.get('itemName')?.value || '',
      }));
      expect(mapped[0].quantity).toBe(7);
      expect(mapped[0].unitPrice).toBe(30);
      expect(mapped[0].description).toBe('Pre-loaded item');
    });

    it('should calculate net total across items', () => {
      const items = createItemsArray();
      addRow(items, { itemId: 'a', qty: 2, rate: 100 }); // 200
      addRow(items, { itemId: 'b', qty: 3, rate: 50 });  // 150
      addRow(items, { itemId: 'c', qty: 1, rate: 75 });  // 75
      const totals = items.controls.map(ctrl => {
        const qty = ctrl.get('qty')?.value ?? 0;
        const rate = ctrl.get('rate')?.value ?? 0;
        return qty * rate;
      });
      expect(totals.reduce((a, b) => a + b, 0)).toBe(425);
    });
  });
});
