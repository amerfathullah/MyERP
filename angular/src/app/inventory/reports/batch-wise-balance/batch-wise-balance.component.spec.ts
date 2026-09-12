import { describe, it, expect } from 'vitest';

interface BatchBalanceRow {
  itemId: string;
  itemName: string;
  batchId: string;
  batchNo: string;
  warehouseId: string;
  warehouseName: string;
  balance: number;
  stockValue: number;
  reservedStockQty: number;
  expiryDate: string | null;
  isExpired: boolean;
  isDisabled: boolean;
}

interface BatchBalanceReport {
  rows: BatchBalanceRow[];
  totalBatches: number;
  totalQuantity: number;
  totalStockValue: number;
  totalReservedStock: number;
  expiredBatchCount: number;
}

describe('BatchWiseBalanceComponent logic', () => {
  function mapReportToCsvRows(report: BatchBalanceReport) {
    return report.rows.map(row => ({
      Item: row.itemName,
      'Batch No': row.batchNo,
      Warehouse: row.warehouseName,
      'Balance Qty': row.balance,
      'Reserved Stock': row.reservedStockQty ?? 0,
      'Stock Value': row.stockValue,
      'Expiry Date': row.expiryDate ?? '',
      Expired: row.isExpired ? 'Yes' : 'No',
      Disabled: row.isDisabled ? 'Yes' : 'No',
    }));
  }

  it('maps reservedStockQty in CSV rows correctly (PR #59008)', () => {
    const report: BatchBalanceReport = {
      rows: [
        {
          itemId: 'item-1',
          itemName: 'Item Alpha',
          batchId: 'b-1',
          batchNo: 'BATCH-001',
          warehouseId: 'wh-1',
          warehouseName: 'Stores',
          balance: 10,
          stockValue: 500,
          reservedStockQty: 4,
          expiryDate: '2027-01-01',
          isExpired: false,
          isDisabled: false,
        },
      ],
      totalBatches: 1,
      totalQuantity: 10,
      totalStockValue: 500,
      totalReservedStock: 4,
      expiredBatchCount: 0,
    };

    const csvRows = mapReportToCsvRows(report);
    expect(csvRows.length).toBe(1);
    expect(csvRows[0]['Reserved Stock']).toBe(4);
    expect(csvRows[0]['Balance Qty']).toBe(10);
    expect(report.totalReservedStock).toBe(4);
  });

  it('defaults reserved stock to zero if missing or null', () => {
    const report: BatchBalanceReport = {
      rows: [
        {
          itemId: 'item-2',
          itemName: 'Item Beta',
          batchId: 'b-2',
          batchNo: 'BATCH-002',
          warehouseId: 'wh-1',
          warehouseName: 'Stores',
          balance: 20,
          stockValue: 1000,
          reservedStockQty: (null as unknown as number),
          expiryDate: null,
          isExpired: false,
          isDisabled: false,
        },
      ],
      totalBatches: 1,
      totalQuantity: 20,
      totalStockValue: 1000,
      totalReservedStock: 0,
      expiredBatchCount: 0,
    };

    const csvRows = mapReportToCsvRows(report);
    expect(csvRows[0]['Reserved Stock']).toBe(0);
  });
});
