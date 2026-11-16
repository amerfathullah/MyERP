import { describe, it, expect } from 'vitest';
import { mapCandidateEntries, mapCandidateRequests } from './payment-order-form.component';

describe('PaymentOrderFormComponent', () => {
  it('maps candidate payment requests and payment entries to reference rows', () => {
    const reqs = mapCandidateRequests([
      {
        id: 'pr-1',
        referenceNumber: 'PREQ-001',
        partyId: 'sup-1',
        partyName: 'Vendor A',
        outstandingAmount: 250,
        grandTotal: 300,
      },
    ]);
    expect(reqs).toHaveLength(1);
    expect(reqs[0].amount).toBe(250);
    expect(reqs[0].referenceType).toBe('PaymentRequest');

    const entries = mapCandidateEntries([
      {
        id: 'pe-1',
        entryNumber: 'PAY-001',
        partyId: 'sup-2',
        partyName: 'Vendor B',
        paidAmount: 400,
        modeOfPayment: 'Cheque',
      },
    ]);
    expect(entries).toHaveLength(1);
    expect(entries[0].amount).toBe(400);
    expect(entries[0].modeOfPayment).toBe('Cheque');
    expect(entries[0].referenceType).toBe('PaymentEntry');
  });
});
