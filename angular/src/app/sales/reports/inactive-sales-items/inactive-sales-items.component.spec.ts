import { describe, it, expect } from 'vitest';
import { filterInactiveSalesItems } from './inactive-sales-items.component';

describe('InactiveSalesItemsComponent logic', () => {
  it('should filter inactive sales items by code, name, group, customer, or territory', () => {
    const items = [
      { itemCode: 'A-1', itemName: 'Desk', itemGroup: 'Furniture', customerName: 'Acme', territory: 'Central' },
      { itemCode: 'B-2', itemName: 'Lamp', itemGroup: 'Lighting', customerName: 'Beta', territory: 'North' },
    ];
    expect(filterInactiveSalesItems(items, '').length).toBe(2);
    expect(filterInactiveSalesItems(items, 'lighting').length).toBe(1);
    expect(filterInactiveSalesItems(items, 'acme')[0].itemCode).toBe('A-1');
  });
});
