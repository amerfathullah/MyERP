import { describe, it, expect } from 'vitest';
import { filterPaymentSummaryRows } from './sales-payment-summary.component';

describe('SalesPaymentSummaryComponent logic', () => {
  it('should filter rows by cashier, warehouse, mode of payment, or cost center', () => {
    const rows = [
      { cashier: 'Ali', warehouseName: 'POS Store', modeOfPayment: 'Cash', costCenter: 'Main', paidAmount: 100 },
      { cashier: 'Siti', warehouseName: 'Online', modeOfPayment: 'Card', costCenter: 'E-Commerce', paidAmount: 250 },
    ];
    expect(filterPaymentSummaryRows(rows, '').length).toBe(2);
    expect(filterPaymentSummaryRows(rows, 'card').length).toBe(1);
    expect(filterPaymentSummaryRows(rows, 'ali')[0].paidAmount).toBe(100);
  });
});
