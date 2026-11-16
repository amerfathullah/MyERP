import { describe, it, expect } from 'vitest';
import { buildGlPlComparisonCsvRows } from './general-payment-ledger-comparison.component';
import type { GeneralPaymentLedgerComparisonRowDto } from '../../../proxy/accounting/models';

describe('GeneralPaymentLedgerComparisonComponent', () => {
  it('formats comparison discrepancy rows for CSV export', () => {
    const rows: GeneralPaymentLedgerComparisonRowDto[] = [
      {
        accountCode: '1200',
        accountName: 'Debtors',
        voucherType: 'SalesInvoice',
        voucherNo: 'SINV-001',
        partyType: 'Customer',
        partyName: 'Beta Sdn Bhd',
        againstVoucherNo: 'SINV-001',
        glBalance: 1000,
        plBalance: 900,
        difference: 100,
      },
    ];

    const csv = buildGlPlComparisonCsvRows(rows);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Account']).toBe('1200 Debtors');
    expect(csv[0]['Difference']).toBe('100.00');
  });
});
