import { describe, it, expect } from 'vitest';
import { filterValuationRows } from './stock-valuation-summary.component';

describe('StockValuationSummaryComponent logic', () => {
  it('should filter rows by item code, item name, or warehouse', () => {
    const rows = [
      { itemCode: 'ITM-01', itemName: 'Widget', warehouseName: 'Main', stockValue: 500 },
      { itemCode: 'ITM-02', itemName: 'Bolt', warehouseName: 'Overflow', stockValue: 120 },
    ];
    expect(filterValuationRows(rows, '').length).toBe(2);
    expect(filterValuationRows(rows, 'widget').length).toBe(1);
    expect(filterValuationRows(rows, 'overflow')[0].itemCode).toBe('ITM-02');
  });
});
