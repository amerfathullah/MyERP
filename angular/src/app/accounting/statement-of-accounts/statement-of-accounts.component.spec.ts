import { describe, it, expect } from 'vitest';
import { buildBatchStatementCsvRows } from './statement-of-accounts.component';
import type { PartyStatementSummaryDto } from '../../proxy/accounting/models';

describe('StatementOfAccountsComponent', () => {
  it('formats batch statement rows with aging buckets for CSV export', () => {
    const rows: PartyStatementSummaryDto[] = [
      {
        partyId: 'c1',
        partyName: 'Alpha Trading Sdn Bhd',
        partyType: 'Customer',
        openingBalance: 100,
        invoicedAmount: 500,
        paidAmount: 200,
        closingBalance: 400,
        aging: {
          current_0_30: 250,
          age_31_60: 150,
          age_61_90: 0,
          age_91_120: 0,
          age_120_Plus: 0,
        },
      },
    ];

    const csv = buildBatchStatementCsvRows(rows);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Party']).toBe('Alpha Trading Sdn Bhd');
    expect(csv[0]['Closing']).toBe('400.00');
    expect(csv[0]['0-30']).toBe('250.00');
    expect(csv[0]['31-60']).toBe('150.00');
  });
});
