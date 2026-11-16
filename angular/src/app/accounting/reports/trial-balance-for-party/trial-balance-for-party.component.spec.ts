import { describe, it, expect } from 'vitest';
import { buildPartyTrialBalanceCsvRows } from './trial-balance-for-party.component';
import type { PartyTrialBalanceRowDto } from '../../../proxy/accounting/models';

describe('TrialBalanceForPartyComponent', () => {
  it('formats party trial balance rows for CSV export', () => {
    const rows: PartyTrialBalanceRowDto[] = [
      {
        partyId: 'p1',
        partyName: 'Acme Sdn Bhd',
        partyType: 'Customer',
        openingDebit: 100,
        openingCredit: 0,
        debit: 500,
        credit: 200,
        closingDebit: 400,
        closingCredit: 0,
        currency: 'MYR',
      },
    ];

    const csv = buildPartyTrialBalanceCsvRows(rows);
    expect(csv).toHaveLength(1);
    expect(csv[0]['Party']).toBe('Acme Sdn Bhd');
    expect(csv[0]['Closing Debit']).toBe('400.00');
  });
});
