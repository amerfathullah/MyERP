import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { ItemDetailComponent } from './item-detail.component';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemAlternativeService } from '../../proxy/inventory/item-alternative.service';
import { ItemLeadTimeService } from '../../proxy/inventory/item-lead-time.service';
import { ItemManufacturerService } from '../../proxy/inventory/item-manufacturer.service';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { StockLedgerService } from '../../proxy/inventory/stock-ledger.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';

describe('ItemDetailComponent Alternatives & Manufacturers', () => {
  function createComponent() {
    const altMock = {
      getAlternatives: vi.fn().mockReturnValue(of([
        { id: 'alt-rel-1', itemId: 'item-1', alternativeItemId: 'item-2', alternativeItemCode: 'ALT-2', alternativeItemName: 'Bracket B', twoWay: true },
        { id: 'alt-rel-2', itemId: 'item-3', alternativeItemId: 'item-1', itemCode: 'SRC-3', itemName: 'Bracket C', twoWay: true },
      ])),
    };
    const mfrMock = {
      getListByItem: vi.fn().mockReturnValue(of([
        { id: 'm-1', manufacturerShortName: 'Bosch', manufacturerPartNo: 'B-900', isDefault: true },
      ])),
    };
    const leadTimeMock = {
      getByItemId: vi.fn().mockReturnValue(of({
        id: 'lt-1',
        itemId: 'item-1',
        purchaseTimeDays: 7,
        bufferTimeDays: 2,
        manufacturingTimeInMins: 30,
        capacityPerDay: 100,
      })),
    };
    const stockLedgerMock = {
      getItemMovementHistory: vi.fn().mockReturnValue(of({
        itemId: 'item-1',
        totalInward: 50,
        totalOutward: 12,
        currentBalance: 38,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { params: { id: 'item-1' } } } },
        { provide: ItemService, useValue: { get: vi.fn().mockReturnValue(of({ id: 'item-1', itemCode: 'I-1', itemName: 'Bracket A' })), getRecentMovements: vi.fn().mockReturnValue(of([])), getPriceHistory: vi.fn().mockReturnValue(of([])), getWhereUsed: vi.fn().mockReturnValue(of([])), getTransactionSummary: vi.fn().mockReturnValue(of(null)) } },
        { provide: ItemAlternativeService, useValue: altMock },
        { provide: ItemLeadTimeService, useValue: leadTimeMock },
        { provide: ItemManufacturerService, useValue: mfrMock },
        { provide: StockBalanceService, useValue: { getItemStock: vi.fn().mockReturnValue(of([])) } },
        { provide: StockEntryService, useValue: {} },
        { provide: StockLedgerService, useValue: stockLedgerMock },
        { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ItemDetailComponent());
    comp.ngOnInit();
    return { comp, altMock, mfrMock, leadTimeMock, stockLedgerMock };
  }

  it('loads item alternatives and manufacturers and resolves two-way target labels', () => {
    const { comp, altMock, mfrMock, leadTimeMock, stockLedgerMock } = createComponent();
    expect(altMock.getAlternatives).toHaveBeenCalledWith('item-1');
    expect(mfrMock.getListByItem).toHaveBeenCalledWith('item-1');
    expect(leadTimeMock.getByItemId).toHaveBeenCalledWith('item-1');
    expect(stockLedgerMock.getItemMovementHistory).toHaveBeenCalledWith('item-1', undefined, 15);
    expect(comp.leadTime()?.purchaseTimeDays).toBe(7);
    expect(comp.movementSummary()?.currentBalance).toBe(38);
    expect(comp.alternatives()).toHaveLength(2);
    expect(comp.manufacturers()).toHaveLength(1);

    expect(comp.getAlternativeTargetId(comp.alternatives()[0])).toBe('item-2');
    expect(comp.getAlternativeTargetLabel(comp.alternatives()[0])).toBe('ALT-2 — Bracket B');
    expect(comp.getAlternativeTargetId(comp.alternatives()[1])).toBe('item-3');
    expect(comp.getAlternativeTargetLabel(comp.alternatives()[1])).toBe('SRC-3 — Bracket C');
  });
});
