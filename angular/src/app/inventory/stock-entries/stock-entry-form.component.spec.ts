import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

/**
 * Tests for the Stock Entry form DTO mapping and validation logic.
 * Specifically tests the warehouse validation and item mapping patterns that had bugs.
 */
describe('StockEntry form DTO mapping', () => {
  const fb = new FormBuilder();

  function createSEForm() {
    return fb.group({
      companyId: ['comp-1', Validators.required],
      entryType: [0],
      sourceWarehouse: [''],
      targetWarehouse: [''],
      postingDate: ['2026-07-20'],
      remarks: [''],
      items: fb.array([]),
    });
  }

  function addItem(items: FormArray, opts?: { itemId?: string; qty?: number; rate?: number }) {
    items.push(fb.group({
      itemId: [opts?.itemId ?? '', Validators.required],
      qty: [opts?.qty ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [opts?.rate ?? 0],
      description: [''],
    }));
  }

  function mapItemsForBackend(items: FormArray): any[] {
    return items.controls.map(ctrl => ({
      itemId: ctrl.get('itemId')?.value,
      quantity: ctrl.get('qty')?.value ?? ctrl.get('quantity')?.value ?? 0,
      rate: ctrl.get('rate')?.value ?? 0,
      description: ctrl.get('description')?.value || '',
    }));
  }

  describe('form creation', () => {
    it('should create form with defaults', () => {
      const form = createSEForm();
      expect(form.get('entryType')?.value).toBe(0);
      expect(form.get('companyId')?.value).toBe('comp-1');
    });

    it('should require companyId', () => {
      const form = createSEForm();
      form.patchValue({ companyId: '' });
      expect(form.get('companyId')?.valid).toBe(false);
    });
  });

  describe('items', () => {
    it('should add item to form array', () => {
      const form = createSEForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', qty: 5, rate: 100 });
      expect(items.length).toBe(1);
    });

    it('should reject empty itemId', () => {
      const form = createSEForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: '', qty: 5 });
      expect(items.at(0).get('itemId')?.valid).toBe(false);
    });

    it('should reject zero quantity', () => {
      const form = createSEForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', qty: 0 });
      expect(items.at(0).get('qty')?.valid).toBe(false);
    });
  });

  describe('DTO mapping', () => {
    it('should map qty field to quantity for backend', () => {
      const form = createSEForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', qty: 10, rate: 50 });
      const mapped = mapItemsForBackend(items);
      expect(mapped[0].quantity).toBe(10);
      expect(mapped[0].itemId).toBe('item-1');
    });

    it('should handle multiple items', () => {
      const form = createSEForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', qty: 3, rate: 100 });
      addItem(items, { itemId: 'item-2', qty: 7, rate: 200 });
      const mapped = mapItemsForBackend(items);
      expect(mapped.length).toBe(2);
      expect(mapped[0].quantity).toBe(3);
      expect(mapped[1].quantity).toBe(7);
    });

    it('should default missing fields', () => {
      const form = createSEForm();
      const items = form.get('items') as FormArray;
      items.push(fb.group({ itemId: ['item-x'], qty: [null], rate: [null], description: [null] }));
      const mapped = mapItemsForBackend(items);
      expect(mapped[0].quantity).toBe(0);
      expect(mapped[0].rate).toBe(0);
      expect(mapped[0].description).toBe('');
    });
  });
});

describe('StockEntry type validation rules', () => {
  // Transfer requires source + target
  // Receipt requires target only
  // Issue requires source only

  function validateWarehouses(entryType: number, source: string, target: string): string[] {
    const errors: string[] = [];
    const TRANSFER_TYPES = [2, 3, 8, 9]; // Transfer, TransferForMfg, SendToWarehouse, ReceiveAtWarehouse
    const RECEIPT_TYPES = [0, 9, 4]; // MaterialReceipt, ReceiveAtWarehouse, Manufacture
    const ISSUE_TYPES = [1]; // MaterialIssue

    if (TRANSFER_TYPES.includes(entryType)) {
      if (!source) errors.push('sourceWarehouseRequired');
      if (!target) errors.push('targetWarehouseRequired');
      if (source && target && source === target) errors.push('sameWarehouse');
    }
    if (RECEIPT_TYPES.includes(entryType) && !target) {
      errors.push('targetWarehouseRequired');
    }
    if (ISSUE_TYPES.includes(entryType) && !source) {
      errors.push('sourceWarehouseRequired');
    }
    return errors;
  }

  it('Transfer requires both warehouses', () => {
    const errors = validateWarehouses(2, '', '');
    expect(errors).toContain('sourceWarehouseRequired');
    expect(errors).toContain('targetWarehouseRequired');
  });

  it('Transfer blocks same warehouse', () => {
    const errors = validateWarehouses(2, 'wh-1', 'wh-1');
    expect(errors).toContain('sameWarehouse');
  });

  it('Transfer allows different warehouses', () => {
    const errors = validateWarehouses(2, 'wh-1', 'wh-2');
    expect(errors.length).toBe(0);
  });

  it('Receipt requires target only', () => {
    const errors = validateWarehouses(0, '', '');
    expect(errors).toContain('targetWarehouseRequired');
  });

  it('Receipt with target is valid', () => {
    const errors = validateWarehouses(0, '', 'wh-target');
    expect(errors.length).toBe(0);
  });

  it('Issue requires source only', () => {
    const errors = validateWarehouses(1, '', 'wh-x');
    expect(errors).toContain('sourceWarehouseRequired');
  });

  it('Issue with source is valid', () => {
    const errors = validateWarehouses(1, 'wh-source', '');
    expect(errors.length).toBe(0);
  });
});
