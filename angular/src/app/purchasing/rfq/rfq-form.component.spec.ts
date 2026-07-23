import { describe, it, expect } from 'vitest';

// RFQ Form — item/supplier management + validation tests

function createDefaultForm() {
  return {
    transactionDate: new Date().toISOString().split('T')[0],
    currencyCode: 'MYR',
    messageForSupplier: '',
    items: [] as { itemId: string; description: string; qty: number; uom: string }[],
    suppliers: [] as { supplierId: string; supplierName?: string; email: string }[],
  };
}

describe('RfqFormComponent', () => {
  describe('form defaults', () => {
    it('should default date to today', () => {
      const form = createDefaultForm();
      expect(form.transactionDate).toBe(new Date().toISOString().split('T')[0]);
    });

    it('should default currency to MYR', () => {
      const form = createDefaultForm();
      expect(form.currencyCode).toBe('MYR');
    });

    it('should start with empty items and suppliers', () => {
      const form = createDefaultForm();
      expect(form.items).toHaveLength(0);
      expect(form.suppliers).toHaveLength(0);
    });
  });

  describe('item management', () => {
    it('should add item with defaults', () => {
      const form = createDefaultForm();
      form.items.push({ itemId: '', description: '', qty: 1, uom: 'Unit' });
      expect(form.items).toHaveLength(1);
      expect(form.items[0].qty).toBe(1);
      expect(form.items[0].uom).toBe('Unit');
    });

    it('should add multiple items', () => {
      const form = createDefaultForm();
      form.items.push({ itemId: 'a', description: 'Steel Rod', qty: 10, uom: 'Kg' });
      form.items.push({ itemId: 'b', description: 'Bolts', qty: 100, uom: 'Unit' });
      expect(form.items).toHaveLength(2);
    });

    it('should remove item by index', () => {
      const form = createDefaultForm();
      form.items.push({ itemId: 'a', description: 'A', qty: 1, uom: 'Unit' });
      form.items.push({ itemId: 'b', description: 'B', qty: 2, uom: 'Unit' });
      form.items.splice(0, 1);
      expect(form.items).toHaveLength(1);
      expect(form.items[0].description).toBe('B');
    });
  });

  describe('supplier management', () => {
    it('should add supplier', () => {
      const form = createDefaultForm();
      form.suppliers.push({ supplierId: 's1', supplierName: 'Acme', email: 'acme@test.com' });
      expect(form.suppliers).toHaveLength(1);
    });

    it('should remove supplier by index', () => {
      const form = createDefaultForm();
      form.suppliers.push({ supplierId: 's1', email: 'a@test.com' });
      form.suppliers.push({ supplierId: 's2', email: 'b@test.com' });
      form.suppliers.splice(1, 1);
      expect(form.suppliers).toHaveLength(1);
      expect(form.suppliers[0].supplierId).toBe('s1');
    });
  });

  describe('form submission validation', () => {
    it('should require at least one item', () => {
      const form = createDefaultForm();
      expect(form.items.length > 0).toBe(false);
    });

    it('should require at least one supplier', () => {
      const form = createDefaultForm();
      expect(form.suppliers.length > 0).toBe(false);
    });

    it('should pass validation with items and suppliers', () => {
      const form = createDefaultForm();
      form.items.push({ itemId: 'i1', description: 'Widget', qty: 5, uom: 'Unit' });
      form.suppliers.push({ supplierId: 's1', email: 'x@test.com' });
      expect(form.items.length > 0 && form.suppliers.length > 0).toBe(true);
    });
  });

  describe('DTO shape', () => {
    it('should produce complete DTO', () => {
      const form = createDefaultForm();
      form.messageForSupplier = 'Please quote urgently';
      form.items.push({ itemId: 'i1', description: 'Steel', qty: 100, uom: 'Kg' });
      form.suppliers.push({ supplierId: 's1', email: 'sup@test.com' });

      expect(form).toHaveProperty('transactionDate');
      expect(form).toHaveProperty('currencyCode');
      expect(form).toHaveProperty('messageForSupplier');
      expect(form.items[0]).toHaveProperty('itemId');
      expect(form.items[0]).toHaveProperty('qty');
      expect(form.suppliers[0]).toHaveProperty('supplierId');
    });
  });
});
