import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

/**
 * Tests for the Delivery Note form DTO mapping.
 * Verifies that items correctly include unitPrice + taxAmount (backend required fields).
 */
describe('DeliveryNote form DTO mapping', () => {
  const fb = new FormBuilder();

  function createDNForm() {
    return fb.group({
      companyId: ['comp-1', Validators.required],
      customerId: ['', Validators.required],
      warehouseId: ['', Validators.required],
      salesOrderId: [''],
      deliveryDate: ['2026-07-20'],
      notes: [''],
      items: fb.array([]),
    });
  }

  function addItem(items: FormArray, opts?: { itemId?: string; qty?: number; unitPrice?: number }) {
    items.push(fb.group({
      itemId: [opts?.itemId ?? '', Validators.required],
      description: [''],
      quantity: [opts?.qty ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [opts?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [0],
      uom: ['Unit'],
    }));
  }

  function mapFormToCreateDto(form: ReturnType<typeof createDNForm>): any {
    const val = form.getRawValue();
    return {
      companyId: val.companyId,
      customerId: val.customerId,
      warehouseId: val.warehouseId,
      salesOrderId: val.salesOrderId || undefined,
      deliveryDate: val.deliveryDate,
      notes: val.notes,
      items: (val.items as any[]).map(item => ({
        itemId: item.itemId,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
        taxAmount: item.taxAmount,
        description: item.description || '',
      })),
    };
  }

  describe('form validation', () => {
    it('should require companyId, customerId, warehouseId', () => {
      const form = createDNForm();
      form.patchValue({ companyId: '', customerId: '', warehouseId: '' });
      expect(form.valid).toBe(false);
    });

    it('should be valid with required fields + items', () => {
      const form = createDNForm();
      form.patchValue({ customerId: 'cust-1', warehouseId: 'wh-1' });
      addItem(form.get('items') as FormArray, { itemId: 'item-1', qty: 5, unitPrice: 100 });
      expect(form.valid).toBe(true);
    });
  });

  describe('DTO mapping', () => {
    it('should include unitPrice in items (backend required)', () => {
      const form = createDNForm();
      form.patchValue({ customerId: 'cust-1', warehouseId: 'wh-1' });
      addItem(form.get('items') as FormArray, { itemId: 'item-1', qty: 2, unitPrice: 150 });
      const dto = mapFormToCreateDto(form);
      expect(dto.items[0].unitPrice).toBe(150);
      expect(dto.items[0].quantity).toBe(2);
    });

    it('should include taxAmount in items', () => {
      const form = createDNForm();
      form.patchValue({ customerId: 'cust-1', warehouseId: 'wh-1' });
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', qty: 1, unitPrice: 100 });
      items.at(0).patchValue({ taxAmount: 6 });
      const dto = mapFormToCreateDto(form);
      expect(dto.items[0].taxAmount).toBe(6);
    });

    it('should exclude empty salesOrderId', () => {
      const form = createDNForm();
      form.patchValue({ customerId: 'cust-1', warehouseId: 'wh-1', salesOrderId: '' });
      addItem(form.get('items') as FormArray, { itemId: 'item-1', qty: 1, unitPrice: 50 });
      const dto = mapFormToCreateDto(form);
      expect(dto.salesOrderId).toBeUndefined();
    });

    it('should include salesOrderId when set', () => {
      const form = createDNForm();
      form.patchValue({ customerId: 'cust-1', warehouseId: 'wh-1', salesOrderId: 'so-abc' });
      addItem(form.get('items') as FormArray, { itemId: 'item-1', qty: 1, unitPrice: 50 });
      const dto = mapFormToCreateDto(form);
      expect(dto.salesOrderId).toBe('so-abc');
    });
  });
});

describe('Invoice item grid DTO field mapping', () => {
  // The grid uses qty/rate/itemName internally, but backend expects quantity/unitPrice/description
  function mapGridItemsToDto(gridItems: any[]): any[] {
    return gridItems.map(item => ({
      itemId: item.itemId,
      quantity: item.quantity ?? item.qty ?? 0,
      unitPrice: item.unitPrice ?? item.rate ?? 0,
      description: item.description || item.itemName || '',
      taxAmount: item.taxAmount ?? 0,
    }));
  }

  it('should map qty→quantity when added from grid (uses qty)', () => {
    const items = [{ itemId: 'x', qty: 5, rate: 100, itemName: 'Widget', taxAmount: 0 }];
    const dto = mapGridItemsToDto(items);
    expect(dto[0].quantity).toBe(5);
    expect(dto[0].unitPrice).toBe(100);
    expect(dto[0].description).toBe('Widget');
  });

  it('should map quantity→quantity when loaded from API (uses quantity)', () => {
    const items = [{ itemId: 'x', quantity: 3, unitPrice: 200, description: 'Loaded item', taxAmount: 6 }];
    const dto = mapGridItemsToDto(items);
    expect(dto[0].quantity).toBe(3);
    expect(dto[0].unitPrice).toBe(200);
    expect(dto[0].description).toBe('Loaded item');
  });

  it('should handle mixed items (some from grid, some from API)', () => {
    const items = [
      { itemId: 'a', qty: 2, rate: 50, itemName: 'From grid' },
      { itemId: 'b', quantity: 10, unitPrice: 30, description: 'From API' },
    ];
    const dto = mapGridItemsToDto(items);
    expect(dto[0].quantity).toBe(2);
    expect(dto[0].unitPrice).toBe(50);
    expect(dto[1].quantity).toBe(10);
    expect(dto[1].unitPrice).toBe(30);
  });

  it('should default to 0 for missing numeric fields', () => {
    const items = [{ itemId: 'x' }];
    const dto = mapGridItemsToDto(items);
    expect(dto[0].quantity).toBe(0);
    expect(dto[0].unitPrice).toBe(0);
    expect(dto[0].taxAmount).toBe(0);
  });

  it('should default to empty string for missing description', () => {
    const items = [{ itemId: 'x', qty: 1 }];
    const dto = mapGridItemsToDto(items);
    expect(dto[0].description).toBe('');
  });
});
