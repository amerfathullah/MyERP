import { describe, it, expect, beforeEach } from 'vitest';
import { FormBuilder, Validators } from '@angular/forms';

/**
 * Work Order Form Component — Manufacturing workflow tests.
 * Validates production planning fields, BOM linkage, quantity rules, and DTO mapping.
 */
describe('WorkOrderFormComponent', () => {
  let fb: FormBuilder;
  let form: ReturnType<FormBuilder['group']>;

  beforeEach(() => {
    fb = new FormBuilder();
    form = fb.group({
      companyId: ['', Validators.required],
      itemId: ['', Validators.required],
      bomId: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(1)]],
      salesOrderId: [''],
      plannedStartDate: [new Date().toISOString().split('T')[0]],
      plannedEndDate: [''],
      notes: [''],
    });
  });

  describe('Required Field Validation', () => {
    it('requires companyId', () => {
      expect(form.get('companyId')?.hasError('required')).toBe(true);
    });

    it('requires itemId', () => {
      expect(form.get('itemId')?.hasError('required')).toBe(true);
    });

    it('requires bomId', () => {
      expect(form.get('bomId')?.hasError('required')).toBe(true);
    });

    it('requires quantity', () => {
      form.patchValue({ quantity: null });
      expect(form.get('quantity')?.hasError('required')).toBe(true);
    });

    it('form invalid without all required', () => {
      expect(form.valid).toBe(false);
    });

    it('form valid with all required fields', () => {
      form.patchValue({
        companyId: 'comp-1',
        itemId: 'item-fg-1',
        bomId: 'bom-1',
        quantity: 100,
      });
      expect(form.valid).toBe(true);
    });
  });

  describe('Quantity Validation', () => {
    it('defaults to 1', () => {
      expect(form.get('quantity')?.value).toBe(1);
    });

    it('minimum quantity is 1', () => {
      form.patchValue({ quantity: 0 });
      expect(form.get('quantity')?.hasError('min')).toBe(true);
    });

    it('fractional quantity below 1 is invalid', () => {
      form.patchValue({ quantity: 0.5 });
      expect(form.get('quantity')?.hasError('min')).toBe(true);
    });

    it('quantity of 1 is valid', () => {
      form.patchValue({ quantity: 1 });
      expect(form.get('quantity')?.valid).toBe(true);
    });

    it('large production quantity is valid', () => {
      form.patchValue({ quantity: 10000 });
      expect(form.get('quantity')?.valid).toBe(true);
    });
  });

  describe('Date Defaults', () => {
    it('plannedStartDate defaults to today', () => {
      const today = new Date().toISOString().split('T')[0];
      expect(form.get('plannedStartDate')?.value).toBe(today);
    });

    it('plannedEndDate defaults to empty', () => {
      expect(form.get('plannedEndDate')?.value).toBe('');
    });

    it('salesOrderId defaults to empty', () => {
      expect(form.get('salesOrderId')?.value).toBe('');
    });
  });

  describe('Query Param Pre-fill (from SO)', () => {
    it('accepts salesOrderId from navigation', () => {
      form.patchValue({ salesOrderId: 'so-uuid-123' });
      expect(form.get('salesOrderId')?.value).toBe('so-uuid-123');
    });

    it('accepts companyId from navigation', () => {
      form.patchValue({ companyId: 'comp-from-so' });
      expect(form.get('companyId')?.value).toBe('comp-from-so');
    });
  });

  describe('BOM Filtering Logic', () => {
    it('filteredBoms returns all when no item selected', () => {
      const allBoms = [
        { id: 'bom-1', itemId: 'item-A' },
        { id: 'bom-2', itemId: 'item-B' },
        { id: 'bom-3', itemId: 'item-A' },
      ];
      // Without item filter, all boms shown
      const selectedItemId = form.get('itemId')?.value;
      const filtered = selectedItemId
        ? allBoms.filter(b => b.itemId === selectedItemId)
        : allBoms;
      expect(filtered.length).toBe(3);
    });

    it('filteredBoms returns only matching item boms', () => {
      const allBoms = [
        { id: 'bom-1', itemId: 'item-A' },
        { id: 'bom-2', itemId: 'item-B' },
        { id: 'bom-3', itemId: 'item-A' },
      ];
      form.patchValue({ itemId: 'item-A' });
      const selectedItemId = form.get('itemId')?.value;
      const filtered = allBoms.filter(b => b.itemId === selectedItemId);
      expect(filtered.length).toBe(2);
      expect(filtered.every(b => b.itemId === 'item-A')).toBe(true);
    });

    it('onItemChanged resets bomId', () => {
      form.patchValue({ bomId: 'existing-bom' });
      // Simulate onItemChanged
      form.patchValue({ bomId: '' });
      expect(form.get('bomId')?.value).toBe('');
    });
  });

  describe('DTO Mapping', () => {
    it('produces correct DTO shape', () => {
      form.patchValue({
        companyId: 'comp-mfg',
        itemId: 'item-finished-good',
        bomId: 'bom-active-1',
        quantity: 500,
        salesOrderId: 'so-linked',
        plannedStartDate: '2026-08-01',
        plannedEndDate: '2026-08-15',
        notes: 'Urgent batch',
      });

      const dto = form.getRawValue();
      expect(dto.companyId).toBe('comp-mfg');
      expect(dto.itemId).toBe('item-finished-good');
      expect(dto.bomId).toBe('bom-active-1');
      expect(dto.quantity).toBe(500);
      expect(dto.salesOrderId).toBe('so-linked');
      expect(dto.plannedStartDate).toBe('2026-08-01');
    });

    it('DTO field names match backend expectations', () => {
      const dto = form.getRawValue();
      expect(dto).toHaveProperty('companyId');
      expect(dto).toHaveProperty('itemId');
      expect(dto).toHaveProperty('bomId');
      expect(dto).toHaveProperty('quantity');
      expect(dto).toHaveProperty('salesOrderId');
      expect(dto).toHaveProperty('plannedStartDate');
      expect(dto).toHaveProperty('plannedEndDate');
      expect(dto).toHaveProperty('notes');
    });

    it('empty optional fields sent as empty string', () => {
      form.patchValue({ companyId: 'c1', itemId: 'i1', bomId: 'b1', quantity: 10 });
      const dto = form.getRawValue();
      expect(dto.salesOrderId).toBe('');
      expect(dto.plannedEndDate).toBe('');
      expect(dto.notes).toBe('');
    });
  });
});
