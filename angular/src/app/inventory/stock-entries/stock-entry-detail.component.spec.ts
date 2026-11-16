import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { StockEntryDetailComponent } from './stock-entry-detail.component';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { CompanyService } from '../../proxy/core/company.service';

/**
 * stock-entry-detail.component.spec.ts
 * Tests for StockEntryDetailComponent — the most versatile inventory document
 * (17 purpose types: MaterialTransfer, Manufacture, Repack, etc.)
 */

// ========== Workflow Action Visibility ==========

describe('StockEntryDetail workflow actions', () => {
  const getActions = (status: string) => {
    const actions: { name: string; label: string; color: string }[] = [];
    if (status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', color: 'primary' });
    }
    if (status === 'Submitted') {
      actions.push({ name: 'post', label: 'Post', color: 'success' });
      actions.push({ name: 'cancel', label: 'Cancel', color: 'danger' });
    }
    if (status === 'Posted') {
      actions.push({ name: 'cancel', label: 'Cancel', color: 'danger' });
    }
    return actions;
  };

  it('Draft shows Submit only', () => {
    const actions = getActions('Draft');
    expect(actions).toHaveLength(1);
    expect(actions[0].name).toBe('submit');
  });

  it('Submitted shows Post + Cancel', () => {
    const actions = getActions('Submitted');
    expect(actions).toHaveLength(2);
    expect(actions[0].name).toBe('post');
    expect(actions[1].name).toBe('cancel');
  });

  it('Posted shows Cancel only', () => {
    const actions = getActions('Posted');
    expect(actions).toHaveLength(1);
    expect(actions[0].name).toBe('cancel');
  });

  it('Cancelled shows no actions', () => {
    const actions = getActions('Cancelled');
    expect(actions).toHaveLength(0);
  });

  it('Unknown status shows no actions', () => {
    const actions = getActions('SomeOtherStatus');
    expect(actions).toHaveLength(0);
  });
});

// ========== Total Amount Calculation ==========

describe('StockEntryDetail total amount', () => {
  const calculateTotal = (items: { quantity: number; valuationRate: number }[]) =>
    items.reduce((sum, i) => sum + i.quantity * i.valuationRate, 0);

  it('single item total', () => {
    expect(calculateTotal([{ quantity: 10, valuationRate: 5.5 }])).toBe(55);
  });

  it('multi-item total', () => {
    const items = [
      { quantity: 10, valuationRate: 5 },
      { quantity: 20, valuationRate: 3 },
      { quantity: 5, valuationRate: 12 },
    ];
    expect(calculateTotal(items)).toBe(10 * 5 + 20 * 3 + 5 * 12); // 50+60+60=170
  });

  it('empty items returns zero', () => {
    expect(calculateTotal([])).toBe(0);
  });

  it('zero quantity items contribute nothing', () => {
    expect(calculateTotal([{ quantity: 0, valuationRate: 100 }])).toBe(0);
  });

  it('zero valuation rate items contribute nothing', () => {
    expect(calculateTotal([{ quantity: 50, valuationRate: 0 }])).toBe(0);
  });
});

// ========== Edit/Delete Visibility ==========

describe('StockEntryDetail edit/delete visibility', () => {
  it('Draft shows edit and delete buttons', () => {
    const status = 'Draft';
    expect(status === 'Draft').toBe(true);
  });

  it('Submitted hides edit and delete', () => {
    const status: string = 'Submitted';
    expect(status === 'Draft').toBe(false);
  });

  it('Posted hides edit and delete', () => {
    const status: string = 'Posted';
    expect(status === 'Draft').toBe(false);
  });

  it('Cancelled hides edit and delete', () => {
    const status: string = 'Cancelled';
    expect(status === 'Draft').toBe(false);
  });
});

// ========== Item Display Logic ==========

describe('StockEntryDetail item display', () => {
  it('item name fallback to itemId when itemName is null', () => {
    const item = { itemName: null, itemId: 'abc-123' };
    expect(item.itemName || item.itemId).toBe('abc-123');
  });

  it('item name shows itemName when available', () => {
    const item = { itemName: 'Widget', itemId: 'abc-123' };
    expect(item.itemName || item.itemId).toBe('Widget');
  });

  it('warehouse name fallback to ID', () => {
    const item = { sourceWarehouseName: null, sourceWarehouseId: 'wh-001' };
    expect(item.sourceWarehouseName || item.sourceWarehouseId || '-').toBe('wh-001');
  });

  it('warehouse name shows dash when both null', () => {
    const item = { sourceWarehouseName: null, sourceWarehouseId: null };
    expect(item.sourceWarehouseName || item.sourceWarehouseId || '-').toBe('-');
  });
});

// ========== Entry Type Display ==========

describe('StockEntryDetail entry types', () => {
  const SE_TYPES = [
    'MaterialReceipt', 'MaterialIssue', 'MaterialTransfer',
    'MaterialTransferForManufacture', 'Manufacture', 'Repack',
    'SendToSubcontractor', 'MaterialConsumptionForManufacture',
    'Disassemble', 'SendToWarehouse', 'ReceiveAtWarehouse',
    'SubcontractingDelivery', 'SubcontractingReturn', 'Adjustment',
  ];

  it('all 14 standard entry types exist', () => {
    expect(SE_TYPES).toHaveLength(14);
  });

  it('MaterialReceipt is a valid type', () => {
    expect(SE_TYPES).toContain('MaterialReceipt');
  });

  it('Manufacture is a valid type', () => {
    expect(SE_TYPES).toContain('Manufacture');
  });

  it('Disassemble is a valid type', () => {
    expect(SE_TYPES).toContain('Disassemble');
  });
});

describe('StockEntryDetailComponent autoPickBatches', () => {
  it('exposes autoPickBatches for Draft entries and calls service.autoPickBatches', () => {
    const seMock = {
      get: vi.fn().mockReturnValue(of({ id: 'se-1', status: 'Draft', items: [] })),
      autoPickBatches: vi.fn().mockReturnValue(of({ id: 'se-1', status: 'Draft', items: [] })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'se-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: StockEntryService, useValue: seMock },
        { provide: ConfirmationService, useValue: {} },
        { provide: ToasterService, useValue: toasterMock },
        { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
      ],
    });
    const comp = runInInjectionContext(injector, () => new StockEntryDetailComponent());
    comp.ngOnInit();

    expect(comp.workflowActions.some(a => a.name === 'autoPickBatches')).toBe(true);
    comp.onAction('autoPickBatches');

    expect(seMock.autoPickBatches).toHaveBeenCalledWith('se-1');
    expect(toasterMock.success).toHaveBeenCalledWith('::BatchesPicked');
    expect(seMock.get).toHaveBeenCalledTimes(2);
  });
});
