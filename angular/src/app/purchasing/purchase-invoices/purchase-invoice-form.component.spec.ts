import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

describe('PurchaseInvoice form DTO mapping', () => {
  const fb = new FormBuilder();

  function createPIForm() {
    return fb.group({
      invoiceNumber: [''],
      companyId: ['comp-1', Validators.required],
      supplierId: ['', Validators.required],
      supplierTin: [''],
      issueDate: ['2026-07-20', Validators.required],
      dueDate: [''],
      currencyCode: ['MYR'],
      notes: [''],
      isReturn: [false],
      returnAgainstId: [null as string | null],
      updateStock: [false],
      warehouseId: [''],
      items: fb.array([]),
    });
  }

  function addItem(items: FormArray, opts?: Partial<{ itemId: string; description: string; quantity: number; unitPrice: number; taxAmount: number; uom: string }>) {
    items.push(fb.group({
      itemId: [opts?.itemId ?? '', Validators.required],
      description: [opts?.description ?? '', Validators.required],
      quantity: [opts?.quantity ?? 1, Validators.required],
      unitPrice: [opts?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [opts?.taxAmount ?? 0],
      uom: [opts?.uom ?? 'EA'],
    }));
  }

  function mapDtoFromForm(form: ReturnType<typeof createPIForm>): any {
    const raw = form.getRawValue() as any;
    return {
      ...raw,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        taxAmount: item.taxAmount ?? 0,
        uom: item.uom ?? 'Unit',
      })),
    };
  }

  describe('form validation', () => {
    it('should require companyId', () => {
      const form = createPIForm();
      form.patchValue({ companyId: '' });
      expect(form.get('companyId')?.valid).toBe(false);
    });

    it('should require supplierId', () => {
      const form = createPIForm();
      expect(form.get('supplierId')?.valid).toBe(false);
    });

    it('should require issueDate', () => {
      const form = createPIForm();
      form.patchValue({ issueDate: '' });
      expect(form.get('issueDate')?.valid).toBe(false);
    });

    it('should default currency to MYR', () => {
      const form = createPIForm();
      expect(form.get('currencyCode')?.value).toBe('MYR');
    });

    it('should default isReturn to false', () => {
      const form = createPIForm();
      expect(form.get('isReturn')?.value).toBe(false);
    });

    it('should default updateStock to false', () => {
      const form = createPIForm();
      expect(form.get('updateStock')?.value).toBe(false);
    });
  });

  describe('item management', () => {
    it('should add item to form array', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', description: 'Widget', quantity: 10, unitPrice: 50 });
      expect(items.length).toBe(1);
      expect(items.at(0).get('quantity')?.value).toBe(10);
    });

    it('should reject empty itemId', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: '' });
      expect(items.at(0).get('itemId')?.valid).toBe(false);
    });

    it('should reject negative unitPrice', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', unitPrice: -10 });
      expect(items.at(0).get('unitPrice')?.valid).toBe(false);
    });

    it('should allow multiple items', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', quantity: 5, unitPrice: 100 });
      addItem(items, { itemId: 'item-2', quantity: 3, unitPrice: 200 });
      expect(items.length).toBe(2);
    });
  });

  describe('DTO mapping', () => {
    it('should map quantity field correctly', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', quantity: 10, unitPrice: 50 });
      const dto = mapDtoFromForm(form);
      expect(dto.items[0].quantity).toBe(10);
      expect(dto.items[0].unitPrice).toBe(50);
    });

    it('should fallback qty to quantity for grid-added items', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      // Simulate grid-added item with qty/rate field names
      items.push(fb.group({ itemId: ['item-1'], qty: [7], rate: [30], itemName: ['Test'], taxAmount: [0], uom: ['EA'] }));
      const raw = form.getRawValue() as any;
      const mapped = (raw.items ?? []).map((item: any) => ({
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        description: item.description || item.itemName || '',
      }));
      expect(mapped[0].quantity).toBe(7);
      expect(mapped[0].unitPrice).toBe(30);
      expect(mapped[0].description).toBe('Test');
    });

    it('should include updateStock and warehouseId', () => {
      const form = createPIForm();
      form.patchValue({ updateStock: true, warehouseId: 'wh-1' });
      const dto = mapDtoFromForm(form);
      expect(dto.updateStock).toBe(true);
      expect(dto.warehouseId).toBe('wh-1');
    });

    it('should preserve isReturn and returnAgainstId', () => {
      const form = createPIForm();
      form.patchValue({ isReturn: true, returnAgainstId: 'pi-orig-001' });
      const dto = mapDtoFromForm(form);
      expect(dto.isReturn).toBe(true);
      expect(dto.returnAgainstId).toBe('pi-orig-001');
    });

    it('should default uom to Unit when missing', () => {
      const form = createPIForm();
      const items = form.get('items') as FormArray;
      items.push(fb.group({ itemId: ['item-1'], quantity: [1], unitPrice: [100], taxAmount: [0], description: ['X'] }));
      const dto = mapDtoFromForm(form);
      expect(dto.items[0].uom).toBe('Unit');
    });
  });

  describe('debit note (return) flow', () => {
    it('should allow negative quantities for returns', () => {
      const form = createPIForm();
      form.patchValue({ isReturn: true, returnAgainstId: 'pi-001' });
      const items = form.get('items') as FormArray;
      addItem(items, { itemId: 'item-1', quantity: -5, unitPrice: 100 });
      // Negative qty is valid for returns
      expect(items.at(0).get('quantity')?.value).toBe(-5);
    });

    it('should set notes for debit note', () => {
      const form = createPIForm();
      form.patchValue({
        isReturn: true,
        returnAgainstId: 'pi-001',
        notes: 'Debit Note against PI-2026-00005',
      });
      expect(form.get('notes')?.value).toContain('Debit Note');
    });
  });
});
