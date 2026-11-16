import { describe, it, expect } from 'vitest';
import { buildItemWisePurchaseCsvRows } from './purchase-register.component';
import type { ItemWisePurchaseRegisterLineDto } from '../../../proxy/purchasing/models';

describe('PurchaseRegisterComponent', () => {
  it('formats item-wise purchase register rows for CSV export', () => {
    const items: ItemWisePurchaseRegisterLineDto[] = [
      {
        invoiceNumber: 'PINV-001',
        postingDate: '2026-11-16',
        supplierName: 'Supply Co',
        itemCode: 'RM-100',
        itemName: 'Steel Rod',
        itemGroup: 'Raw Material',
        stockQty: 10,
        stockUom: 'Unit',
        rate: 25,
        amount: 250,
        totalTax: 15,
        totalOtherCharges: 5,
        total: 270,
        purchaseOrderNumber: 'PO-001',
        purchaseReceiptNumber: 'PR-001',
      },
    ];

    const csv = buildItemWisePurchaseCsvRows(items);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Item Code']).toBe('RM-100');
    expect(csv[0]['Total']).toBe('270.00');
  });
});
