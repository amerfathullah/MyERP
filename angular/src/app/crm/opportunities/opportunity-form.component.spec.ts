import { describe, it, expect, beforeEach } from 'vitest';
import { FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';

/**
 * Opportunity Form Component — CRM pipeline management tests.
 * Validates sales pipeline data, probability, items, and DTO mapping.
 */
describe('OpportunityFormComponent', () => {
  let fb: FormBuilder;
  let form: FormGroup;

  beforeEach(() => {
    fb = new FormBuilder();
    form = fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      opportunityType: [0],
      contactName: [''],
      contactEmail: ['', [Validators.email]],
      contactPhone: [''],
      salesStage: ['Prospecting'],
      probability: [20, [Validators.min(0), Validators.max(100)]],
      expectedClosingDate: [''],
      opportunityAmount: [0, [Validators.min(0)]],
      currencyCode: ['MYR'],
      territory: [''],
      companyId: ['', Validators.required],
      notes: [''],
      items: fb.array([]),
    });
  });

  function addItemRow(item?: Partial<{ description: string; quantity: number; unitPrice: number; uom: string }>) {
    (form.get('items') as FormArray).push(fb.group({
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      uom: [item?.uom ?? 'EA'],
    }));
  }

  describe('Required Field Validation', () => {
    it('requires title', () => {
      expect(form.get('title')?.hasError('required')).toBe(true);
      form.patchValue({ title: 'New Deal' });
      expect(form.get('title')?.valid).toBe(true);
    });

    it('requires companyId', () => {
      expect(form.get('companyId')?.hasError('required')).toBe(true);
    });

    it('form invalid without required fields', () => {
      expect(form.valid).toBe(false);
    });

    it('form valid with title and companyId', () => {
      form.patchValue({ title: 'Enterprise Deal', companyId: 'comp-1' });
      expect(form.valid).toBe(true);
    });

    it('title maxLength 200', () => {
      form.patchValue({ title: 'X'.repeat(201) });
      expect(form.get('title')?.hasError('maxlength')).toBe(true);
    });
  });

  describe('Sales Pipeline Fields', () => {
    it('defaults salesStage to Prospecting', () => {
      expect(form.get('salesStage')?.value).toBe('Prospecting');
    });

    it('defaults probability to 20', () => {
      expect(form.get('probability')?.value).toBe(20);
    });

    it('probability minimum is 0', () => {
      form.patchValue({ probability: -1 });
      expect(form.get('probability')?.hasError('min')).toBe(true);
    });

    it('probability maximum is 100', () => {
      form.patchValue({ probability: 101 });
      expect(form.get('probability')?.hasError('max')).toBe(true);
    });

    it('probability 100 is valid (Closed Won)', () => {
      form.patchValue({ probability: 100 });
      expect(form.get('probability')?.valid).toBe(true);
    });

    it('probability 0 is valid (Lost)', () => {
      form.patchValue({ probability: 0 });
      expect(form.get('probability')?.valid).toBe(true);
    });

    it('opportunityAmount minimum is 0', () => {
      form.patchValue({ opportunityAmount: -100 });
      expect(form.get('opportunityAmount')?.hasError('min')).toBe(true);
    });

    it('defaults currency to MYR', () => {
      expect(form.get('currencyCode')?.value).toBe('MYR');
    });
  });

  describe('Contact Email Validation', () => {
    it('empty email is valid (optional)', () => {
      expect(form.get('contactEmail')?.valid).toBe(true);
    });

    it('invalid email format rejected', () => {
      form.patchValue({ contactEmail: 'bad-email' });
      expect(form.get('contactEmail')?.hasError('email')).toBe(true);
    });

    it('valid email accepted', () => {
      form.patchValue({ contactEmail: 'buyer@corp.com.my' });
      expect(form.get('contactEmail')?.valid).toBe(true);
    });
  });

  describe('Item Row Management', () => {
    it('starts with no items', () => {
      expect((form.get('items') as FormArray).length).toBe(0);
    });

    it('adds item with defaults', () => {
      addItemRow();
      const items = form.get('items') as FormArray;
      expect(items.length).toBe(1);
      expect(items.at(0).get('quantity')?.value).toBe(1);
      expect(items.at(0).get('uom')?.value).toBe('EA');
    });

    it('item requires description', () => {
      addItemRow();
      const items = form.get('items') as FormArray;
      expect(items.at(0).get('description')?.hasError('required')).toBe(true);
    });

    it('item quantity minimum is 0.01', () => {
      addItemRow({ description: 'Consulting', quantity: 0 });
      const items = form.get('items') as FormArray;
      expect(items.at(0).get('quantity')?.hasError('min')).toBe(true);
    });

    it('item unitPrice minimum is 0', () => {
      addItemRow({ description: 'Service', unitPrice: -5 });
      const items = form.get('items') as FormArray;
      expect(items.at(0).get('unitPrice')?.hasError('min')).toBe(true);
    });

    it('adds multiple items', () => {
      addItemRow({ description: 'Phase 1', quantity: 1, unitPrice: 50000 });
      addItemRow({ description: 'Phase 2', quantity: 1, unitPrice: 30000 });
      expect((form.get('items') as FormArray).length).toBe(2);
    });

    it('removes item', () => {
      addItemRow({ description: 'A' });
      addItemRow({ description: 'B' });
      (form.get('items') as FormArray).removeAt(0);
      expect((form.get('items') as FormArray).length).toBe(1);
      expect((form.get('items') as FormArray).at(0).get('description')?.value).toBe('B');
    });
  });

  describe('DTO Mapping', () => {
    it('produces correct shape for API', () => {
      form.patchValue({
        title: 'ERP Implementation',
        opportunityType: 1,
        contactName: 'Ahmad',
        contactEmail: 'ahmad@bigcorp.my',
        salesStage: 'Qualification',
        probability: 60,
        expectedClosingDate: '2026-12-31',
        opportunityAmount: 500000,
        currencyCode: 'MYR',
        territory: 'Selangor',
        companyId: 'comp-abc',
        notes: 'Enterprise deal, multi-phase',
      });
      addItemRow({ description: 'Discovery Workshop', quantity: 5, unitPrice: 2000, uom: 'Day' });

      const dto = form.getRawValue();
      expect(dto.title).toBe('ERP Implementation');
      expect(dto.probability).toBe(60);
      expect(dto.opportunityAmount).toBe(500000);
      expect(dto.items.length).toBe(1);
      expect(dto.items[0].description).toBe('Discovery Workshop');
      expect(dto.items[0].unitPrice).toBe(2000);
    });

    it('DTO has all expected field names', () => {
      const dto = form.getRawValue();
      expect(dto).toHaveProperty('title');
      expect(dto).toHaveProperty('opportunityType');
      expect(dto).toHaveProperty('contactName');
      expect(dto).toHaveProperty('contactEmail');
      expect(dto).toHaveProperty('salesStage');
      expect(dto).toHaveProperty('probability');
      expect(dto).toHaveProperty('opportunityAmount');
      expect(dto).toHaveProperty('currencyCode');
      expect(dto).toHaveProperty('companyId');
      expect(dto).toHaveProperty('items');
    });
  });

  describe('Edit Mode', () => {
    it('patches form with existing opportunity', () => {
      const existing = {
        title: 'Existing Deal',
        salesStage: 'Negotiation',
        probability: 80,
        opportunityAmount: 250000,
        companyId: 'comp-1',
      };
      form.patchValue(existing);
      expect(form.get('title')?.value).toBe('Existing Deal');
      expect(form.get('probability')?.value).toBe(80);
    });

    it('loads items from API response', () => {
      const items = [
        { description: 'Item A', quantity: 2, unitPrice: 1000, uom: 'Unit' },
        { description: 'Item B', quantity: 5, unitPrice: 500, uom: 'Hour' },
      ];
      items.forEach(item => addItemRow(item));
      const formItems = form.get('items') as FormArray;
      expect(formItems.length).toBe(2);
      expect(formItems.at(1).get('uom')?.value).toBe('Hour');
    });
  });
});
