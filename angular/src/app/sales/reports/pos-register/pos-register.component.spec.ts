import { describe, it, expect } from 'vitest';
import { countPosTransactions, filterPosRegisterRows } from './pos-register.component';
import type { PosRegisterRowDto } from '../../../proxy/sales/models';

describe('PosRegisterComponent', () => {
  const sampleRows: PosRegisterRowDto[] = [
    {
      invoiceNumber: 'POS-0001',
      customerName: 'Walk-in Customer',
      posProfileName: 'KL Flagship Counter',
      cashierName: 'cashier1',
      modeOfPayment: 'Cash',
      grandTotal: 150,
      paidAmount: 150,
      isSubtotal: false,
    },
    {
      invoiceNumber: 'POS-0002',
      customerName: 'Amina Store',
      posProfileName: 'Penang Kiosk',
      cashierName: 'cashier2',
      modeOfPayment: 'QR Pay',
      grandTotal: 85,
      paidAmount: 85,
      isSubtotal: false,
    },
    {
      invoiceNumber: 'Total - KL Flagship Counter',
      groupKey: 'KL Flagship Counter',
      grandTotal: 150,
      paidAmount: 150,
      isSubtotal: true,
    },
  ];

  it('filters rows by invoice, customer, profile, cashier, or payment method', () => {
    expect(filterPosRegisterRows(undefined, '')).toEqual([]);
    expect(filterPosRegisterRows(sampleRows, '')).toHaveLength(3);
    expect(filterPosRegisterRows(sampleRows, 'qr pay')).toHaveLength(1);
    expect(filterPosRegisterRows(sampleRows, 'penang')[0].invoiceNumber).toBe('POS-0002');
  });

  it('counts non-subtotal transactions accurately', () => {
    expect(countPosTransactions(undefined)).toBe(0);
    expect(countPosTransactions(sampleRows)).toBe(2);
  });
});
