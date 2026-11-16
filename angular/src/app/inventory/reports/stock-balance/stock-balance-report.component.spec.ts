import { describe, it, expect } from 'vitest';
import { filterMovementRows } from './stock-balance-report.component';
import type { StockBalanceReportRowDto } from '../../../proxy/inventory/models';

describe('StockBalanceReportComponent', () => {
  it('filters period stock movement rows by item code, name, or batch', () => {
    const rows: StockBalanceReportRowDto[] = [
      { itemCode: 'RM-01', itemName: 'Copper Coil', batchNo: 'BATCH-A', balanceQty: 20 },
      { itemCode: 'FG-02', itemName: 'Motor Unit', batchNo: 'BATCH-B', balanceQty: 5 },
    ];

    expect(filterMovementRows(rows, '')).toHaveLength(2);
    expect(filterMovementRows(rows, 'copper')).toHaveLength(1);
    expect(filterMovementRows(rows, 'batch-b')[0].itemCode).toBe('FG-02');
  });
});
