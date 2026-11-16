import { describe, it, expect } from 'vitest';
import { mapReconciliationPreviewRows } from './stock-reconciliation-form.component';

describe('StockReconciliationFormComponent', () => {
  it('maps warehouse reconciliation previews with current quantity and valuation rate', () => {
    const rows = mapReconciliationPreviewRows([
      {
        itemId: 'item-1',
        itemCode: 'RM-01',
        itemName: 'Steel Bar',
        warehouseId: 'wh-1',
        currentQuantity: 25,
        currentValuationRate: 14.5,
      },
    ]);

    expect(rows).toHaveLength(1);
    expect(rows[0].currentQuantity).toBe(25);
    expect(rows[0].currentValuationRate).toBe(14.5);
    expect(rows[0].newQuantity).toBe(25);
    expect(rows[0].newValuationRate).toBe(14.5);
  });
});
