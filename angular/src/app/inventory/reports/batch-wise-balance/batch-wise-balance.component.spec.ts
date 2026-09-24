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
  valuationRate?: number;
  serialNos?: string | null;
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
      'Serial Nos': row.serialNos ?? '',
      Warehouse: row.warehouseName,
      'Balance Qty': row.balance,
      'Valuation Rate': row.valuationRate ?? 0,
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

  it('maps serialNos and valuationRate in CSV rows correctly (PR #59321)', () => {
    const report: BatchBalanceReport = {
      rows: [
        {
          itemId: 'item-3',
          itemName: 'Item Gamma',
          batchId: '00000000-0000-0000-0000-000000000000',
          batchNo: '(Serialized)',
          warehouseId: 'wh-1',
          warehouseName: 'Stores',
          balance: 2,
          stockValue: 200,
          valuationRate: 100,
          serialNos: 'SN-001, SN-002',
          reservedStockQty: 0,
          expiryDate: null,
          isExpired: false,
          isDisabled: false,
        },
      ],
      totalBatches: 0,
      totalQuantity: 2,
      totalStockValue: 200,
      totalReservedStock: 0,
      expiredBatchCount: 0,
    };

    const csvRows = mapReportToCsvRows(report);
    expect(csvRows.length).toBe(1);
    expect(csvRows[0]['Serial Nos']).toBe('SN-001, SN-002');
    expect(csvRows[0]['Valuation Rate']).toBe(100);
    expect(csvRows[0]['Batch No']).toBe('(Serialized)');
  });
});

