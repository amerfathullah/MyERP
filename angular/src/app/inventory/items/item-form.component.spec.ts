import { describe, it, expect } from 'vitest';
import { FormBuilder, Validators } from '@angular/forms';

/**
 * Tests for Item form validation, defaults, and DTO mapping.
 * The Item form is the most complex master data form with:
 * - Stock settings (UOM, maintain stock, reorder)
 * - Quality inspection flags
 * - Pricing (selling/buying)
 * - Inventory configuration
 */
describe('Item form logic', () => {
  const fb = new FormBuilder();

  function createItemForm() {
    return fb.group({
      companyId: ['', Validators.required],
      itemCode: ['', [Validators.required, Validators.maxLength(50)]],
      itemName: ['', [Validators.required, Validators.maxLength(200)]],
      description: [''],
      itemType: [0, Validators.required],
      itemGroup: [''],
      uom: ['Unit'],
      standardSellingPrice: [0],
      standardBuyingPrice: [0],
      maintainStock: [true],
      isActive: [true],
      reorderLevel: [0],
      reorderQty: [0],
      safetyStock: [0],
      minOrderQty: [0],
      inspectionRequiredBeforePurchase: [false],
      inspectionRequiredBeforeDelivery: [false],
    });
  }

  describe('validation', () => {
    it('should require companyId', () => {
      const form = createItemForm();
      expect(form.get('companyId')?.valid).toBe(false);
    });

    it('should require itemCode', () => {
      const form = createItemForm();
      expect(form.get('itemCode')?.valid).toBe(false);
    });

    it('should require itemName', () => {
      const form = createItemForm();
      expect(form.get('itemName')?.valid).toBe(false);
    });

    it('should reject itemCode over 50 chars', () => {
      const form = createItemForm();
      form.patchValue({ itemCode: 'A'.repeat(51) });
      expect(form.get('itemCode')?.valid).toBe(false);
    });

    it('should reject itemName over 200 chars', () => {
      const form = createItemForm();
      form.patchValue({ itemName: 'A'.repeat(201) });
      expect(form.get('itemName')?.valid).toBe(false);
    });

    it('should be valid with required fields', () => {
      const form = createItemForm();
      form.patchValue({ companyId: 'comp-1', itemCode: 'ITEM-001', itemName: 'Test Widget' });
      expect(form.valid).toBe(true);
    });
  });

  describe('defaults', () => {
    it('should default itemType to 0 (Goods)', () => {
      const form = createItemForm();
      expect(form.get('itemType')?.value).toBe(0);
    });

    it('should default UOM to Unit', () => {
      const form = createItemForm();
      expect(form.get('uom')?.value).toBe('Unit');
    });

    it('should default maintainStock to true', () => {
      const form = createItemForm();
      expect(form.get('maintainStock')?.value).toBe(true);
    });

    it('should default isActive to true', () => {
      const form = createItemForm();
      expect(form.get('isActive')?.value).toBe(true);
    });

    it('should default reorder settings to 0', () => {
      const form = createItemForm();
      expect(form.get('reorderLevel')?.value).toBe(0);
      expect(form.get('reorderQty')?.value).toBe(0);
      expect(form.get('safetyStock')?.value).toBe(0);
    });

    it('should default inspection flags to false', () => {
      const form = createItemForm();
      expect(form.get('inspectionRequiredBeforePurchase')?.value).toBe(false);
      expect(form.get('inspectionRequiredBeforeDelivery')?.value).toBe(false);
    });

    it('should default prices to 0', () => {
      const form = createItemForm();
      expect(form.get('standardSellingPrice')?.value).toBe(0);
      expect(form.get('standardBuyingPrice')?.value).toBe(0);
    });
  });

  describe('DTO mapping', () => {
    it('should produce complete DTO with all fields', () => {
      const form = createItemForm();
      form.patchValue({
        companyId: 'comp-1',
        itemCode: 'WIDGET-001',
        itemName: 'Steel Widget',
        description: 'A high-quality steel widget',
        itemType: 0,
        itemGroup: 'Raw Materials',
        uom: 'Kg',
        standardSellingPrice: 150,
        standardBuyingPrice: 80,
        maintainStock: true,
        reorderLevel: 50,
        reorderQty: 200,
        safetyStock: 25,
      });

      const dto = form.getRawValue();
      expect(dto.itemCode).toBe('WIDGET-001');
      expect(dto.uom).toBe('Kg');
      expect(dto.standardSellingPrice).toBe(150);
      expect(dto.standardBuyingPrice).toBe(80);
      expect(dto.reorderLevel).toBe(50);
      expect(dto.reorderQty).toBe(200);
    });

    it('should include quality inspection flags', () => {
      const form = createItemForm();
      form.patchValue({
        companyId: 'c', itemCode: 'IC', itemName: 'N',
        inspectionRequiredBeforePurchase: true,
        inspectionRequiredBeforeDelivery: true,
      });

      const dto = form.getRawValue();
      expect(dto.inspectionRequiredBeforePurchase).toBe(true);
      expect(dto.inspectionRequiredBeforeDelivery).toBe(true);
    });

    it('should differentiate item types', () => {
      const form = createItemForm();
      // Service item
      form.patchValue({ companyId: 'c', itemCode: 'SVC', itemName: 'Consulting', itemType: 1 });
      expect(form.getRawValue().itemType).toBe(1);

      // Fixed Asset
      form.patchValue({ itemType: 2 });
      expect(form.getRawValue().itemType).toBe(2);
    });
  });

  describe('edit mode behavior', () => {
    it('should patch all fields from API response', () => {
      const form = createItemForm();
      const apiResponse = {
        companyId: 'comp-1',
        itemCode: 'BOLT-M8',
        itemName: 'M8 Hex Bolt',
        description: 'Stainless steel',
        itemType: 0,
        itemGroup: 'Fasteners',
        uom: 'Dozen',
        standardSellingPrice: 24,
        standardBuyingPrice: 12,
        maintainStock: true,
        isActive: true,
        reorderLevel: 100,
        reorderQty: 500,
        safetyStock: 50,
        minOrderQty: 100,
        inspectionRequiredBeforePurchase: true,
        inspectionRequiredBeforeDelivery: false,
      };

      form.patchValue(apiResponse);

      const dto = form.getRawValue();
      expect(dto.itemCode).toBe('BOLT-M8');
      expect(dto.uom).toBe('Dozen');
      expect(dto.reorderLevel).toBe(100);
      expect(dto.minOrderQty).toBe(100);
      expect(dto.inspectionRequiredBeforePurchase).toBe(true);
    });
  });

  describe('reorder logic', () => {
    it('should allow zero reorderLevel (disabled)', () => {
      const form = createItemForm();
      form.patchValue({ companyId: 'c', itemCode: 'I', itemName: 'N', reorderLevel: 0 });
      expect(form.valid).toBe(true);
    });

    it('should support fractional reorder quantities', () => {
      const form = createItemForm();
      form.patchValue({ reorderLevel: 2.5, reorderQty: 10.75 });
      expect(form.get('reorderLevel')?.value).toBe(2.5);
      expect(form.get('reorderQty')?.value).toBe(10.75);
    });
  });
});
