import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';
import { TaxCalculationService } from '../../shared/services/tax-calculation.service';

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
});
