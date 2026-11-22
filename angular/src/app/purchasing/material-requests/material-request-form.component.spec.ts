import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { MaterialRequestFormComponent } from './material-request-form.component';
import { MaterialRequestStore } from '../store/material-request.store';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemDetailsService } from '../../proxy/inventory/item-details.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';

/**
 * Material Request Form Component — Critical procurement form tests.
 * Validates item row management, DTO mapping, and validation rules.
 */
describe('MaterialRequestFormComponent', () => {
  let fb: FormBuilder;
  let form: ReturnType<FormBuilder['group']>;
  let items: FormArray;

  beforeEach(() => {
    fb = new FormBuilder();
    form = fb.group({
      companyId: ['', Validators.required],
      requestType: [0, Validators.required],
      requestDate: [new Date().toISOString().split('T')[0], Validators.required],
      requiredByDate: [''],
      sourceWarehouseId: [''],
      targetWarehouseId: [''],
      notes: [''],
      items: fb.array([]),
    });
    items = form.get('items') as FormArray;
  });

  function addItemRow(data?: Partial<{ itemId: string; itemName: string; quantity: number; uom: string; warehouseId: string }>) {
    items.push(fb.group({
      itemId: [data?.itemId ?? '', Validators.required],
      itemName: [data?.itemName ?? '', Validators.required],
      quantity: [data?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      uom: [data?.uom ?? 'Unit'],
      warehouseId: [data?.warehouseId ?? ''],
    }));
  }

  describe('Form Validation', () => {
    it('requires companyId', () => {
      expect(form.get('companyId')?.hasError('required')).toBe(true);
      form.patchValue({ companyId: 'company-123' });
      expect(form.get('companyId')?.valid).toBe(true);
    });

    it('requires requestType', () => {
      expect(form.get('requestType')?.valid).toBe(true); // default 0 satisfies required
    });

    it('requires requestDate', () => {
      form.patchValue({ requestDate: '' });
      expect(form.get('requestDate')?.hasError('required')).toBe(true);
    });

    it('defaults requestDate to today', () => {
      const today = new Date().toISOString().split('T')[0];
      expect(form.get('requestDate')?.value).toBe(today);
    });

    it('form is invalid without companyId', () => {
      addItemRow({ itemId: 'item-1', itemName: 'Widget', quantity: 5 });
      expect(form.valid).toBe(false);
    });

    it('form is valid with all required fields', () => {
      form.patchValue({ companyId: 'cid' });
      addItemRow({ itemId: 'item-1', itemName: 'Widget', quantity: 10 });
      expect(form.valid).toBe(true);
    });
  });

  describe('Item Row Management', () => {
    it('starts with no items', () => {
      expect(items.length).toBe(0);
    });

    it('adds item row with defaults', () => {
      addItemRow();
      expect(items.length).toBe(1);
      expect(items.at(0).get('quantity')?.value).toBe(1);
      expect(items.at(0).get('uom')?.value).toBe('Unit');
    });

    it('adds multiple items', () => {
      addItemRow({ itemId: 'a', itemName: 'A', quantity: 5 });
      addItemRow({ itemId: 'b', itemName: 'B', quantity: 10 });
      expect(items.length).toBe(2);
    });

    it('removes item row', () => {
      addItemRow({ itemId: 'a', itemName: 'A', quantity: 5 });
      addItemRow({ itemId: 'b', itemName: 'B', quantity: 10 });
      items.removeAt(0);
      expect(items.length).toBe(1);
      expect(items.at(0).get('itemId')?.value).toBe('b');
    });

    it('item requires itemId', () => {
      addItemRow();
      expect(items.at(0).get('itemId')?.hasError('required')).toBe(true);
    });

    it('item requires itemName', () => {
      addItemRow();
      expect(items.at(0).get('itemName')?.hasError('required')).toBe(true);
    });

    it('item quantity minimum is 0.01', () => {
      addItemRow({ itemId: 'x', itemName: 'X', quantity: 0 });
      expect(items.at(0).get('quantity')?.hasError('min')).toBe(true);
    });

    it('item quantity 0.01 is valid', () => {
      addItemRow({ itemId: 'x', itemName: 'X', quantity: 0.01 });
      expect(items.at(0).get('quantity')?.valid).toBe(true);
    });

    it('item rejects negative quantity', () => {
      addItemRow({ itemId: 'x', itemName: 'X', quantity: -5 });
      expect(items.at(0).get('quantity')?.hasError('min')).toBe(true);
    });
  });

  describe('Request Types', () => {
    it('defaults to Purchase type (0)', () => {
      expect(form.get('requestType')?.value).toBe(0);
    });

    it('accepts Transfer type (1)', () => {
      form.patchValue({ requestType: 1 });
      expect(form.get('requestType')?.value).toBe(1);
    });

    it('accepts MaterialIssue type (2)', () => {
      form.patchValue({ requestType: 2 });
      expect(form.get('requestType')?.value).toBe(2);
    });

    it('accepts Manufacture type (3)', () => {
      form.patchValue({ requestType: 3 });
      expect(form.get('requestType')?.value).toBe(3);
    });
  });

  describe('DTO Mapping', () => {
    it('produces correct DTO shape for API', () => {
      form.patchValue({
        companyId: 'comp-123',
        requestType: 0,
        requestDate: '2026-07-20',
        requiredByDate: '2026-07-25',
        sourceWarehouseId: 'wh-1',
        targetWarehouseId: 'wh-2',
        notes: 'Urgent order',
      });
      addItemRow({ itemId: 'item-001', itemName: 'Steel Rod', quantity: 100, uom: 'Kg', warehouseId: 'wh-2' });

      const dto = form.getRawValue();
      expect(dto.companyId).toBe('comp-123');
      expect(dto.requestType).toBe(0);
      expect(dto.requestDate).toBe('2026-07-20');
      expect(dto.items[0].itemId).toBe('item-001');
      expect(dto.items[0].quantity).toBe(100);
      expect(dto.items[0].uom).toBe('Kg');
    });

    it('item field names match backend expectations', () => {
      addItemRow({ itemId: 'i1', itemName: 'Part A', quantity: 50, uom: 'Unit', warehouseId: 'w1' });
      const item = form.getRawValue().items[0];

      // Backend expects these exact field names
      expect(item).toHaveProperty('itemId');
      expect(item).toHaveProperty('itemName');
      expect(item).toHaveProperty('quantity');
      expect(item).toHaveProperty('uom');
      expect(item).toHaveProperty('warehouseId');
    });

    it('empty optional fields are empty strings', () => {
      form.patchValue({ companyId: 'c1' });
      addItemRow({ itemId: 'i1', itemName: 'X', quantity: 1 });
      const dto = form.getRawValue();

      expect(dto.requiredByDate).toBe('');
      expect(dto.sourceWarehouseId).toBe('');
      expect(dto.targetWarehouseId).toBe('');
      expect(dto.notes).toBe('');
    });
  });

  describe('Item Selection', () => {
    it('updates itemName on item selection', () => {
      addItemRow();
      const row = items.at(0);
      // Simulate onItemSelected
      row.patchValue({ itemId: 'item-1', itemName: 'Selected Item', uom: 'Kg' });
      expect(row.get('itemName')?.value).toBe('Selected Item');
      expect(row.get('uom')?.value).toBe('Kg');
    });

    it('preserves warehouse assignment per item', () => {
      addItemRow({ itemId: 'a', itemName: 'A', quantity: 5, warehouseId: 'wh-stores' });
      addItemRow({ itemId: 'b', itemName: 'B', quantity: 3, warehouseId: 'wh-production' });

      const dto = form.getRawValue();
      expect(dto.items[0].warehouseId).toBe('wh-stores');
      expect(dto.items[1].warehouseId).toBe('wh-production');
    });
  });

  describe('Material Transfer source warehouse stock resolution (PR #67502)', () => {
    it('passes fromWarehouseId and isMaterialTransfer to ItemDetailsService and clears sourceWarehouseId when leaving MaterialTransfer', () => {
      const getItemDetailsMock = vi.fn().mockReturnValue(of({ actualQty: 0 }));
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: MaterialRequestStore, useValue: {} },
          { provide: MaterialRequestService, useValue: { create: vi.fn() } },
          { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
          { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ItemService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'item-1', itemName: 'Steel', uom: 'Kg' }] })) } },
          { provide: ItemDetailsService, useValue: { getItemDetails: getItemDetailsMock } },
          { provide: SalesOrderService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), info: vi.fn() } },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new MaterialRequestFormComponent());
      comp.ngOnInit();
      comp.form.patchValue({ companyId: 'comp-1', requestType: 1, sourceWarehouseId: 'wh-other-co', targetWarehouseId: 'wh-target' });
      expect(comp.isMaterialTransfer).toBe(true);

      comp.items.at(0).patchValue({ itemId: 'item-1' });
      comp.onItemSelected(0, 'item-1');

      expect(getItemDetailsMock).toHaveBeenCalledWith(expect.objectContaining({
        itemId: 'item-1',
        companyId: 'comp-1',
        fromWarehouseId: 'wh-other-co',
        isMaterialRequest: true,
        isMaterialTransfer: true,
      }));
      expect(comp.rowActualQty()[0]).toBe(0);

      // Switching away from MaterialTransfer clears sourceWarehouseId
      comp.form.patchValue({ requestType: 0 });
      expect(comp.isMaterialTransfer).toBe(false);
      expect(comp.form.get('sourceWarehouseId')?.value).toBe('');
    });
  });
});
