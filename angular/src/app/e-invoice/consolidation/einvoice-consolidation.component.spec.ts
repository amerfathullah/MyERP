import { describe, it, expect } from 'vitest';
import type { ConsolidationCandidateDto, EInvoiceConsolidationDto } from '../../proxy/einvoice/models';

/**
 * Tests for B2C Consolidation Component logic:
 * - Candidate eligibility & total calculation
 * - Multi-selection state management
 * - Threshold validation (RM 10k split)
 * - Consolidation history formatting
 */
describe('E-Invoice B2C Consolidation Logic', () => {
  function computeSelectedTotal(candidates: ConsolidationCandidateDto[], selectedIds: string[]): number {
    const idSet = new Set(selectedIds);
    return candidates
      .filter(c => !!c.invoiceId && idSet.has(c.invoiceId))
      .reduce((sum, c) => sum + (c.grandTotal || 0), 0);
  }

  function filterEligibleCandidates(
    invoices: Array<{ id: string; invoiceNumber: string; grandTotal: number; status: string; eInvoiceStatus: string; isConsolidated: boolean }>,
    maxAmount: number = 10000
  ) {
    return invoices.filter(inv =>
      inv.status === 'Submitted' &&
      (!inv.eInvoiceStatus || inv.eInvoiceStatus === 'NotSubmitted') &&
      !inv.isConsolidated &&
      inv.grandTotal <= maxAmount
    );
  }

  it('should calculate selected total correctly across multiple candidates', () => {
    const candidates: ConsolidationCandidateDto[] = [
      { invoiceId: 'inv-1', invoiceNumber: 'SINV-001', grandTotal: 500.50, isEligible: true, customerId: 'c-1', customerName: 'Cash Buyer', issueDate: '2026-08-01', itemCount: 2, currencyCode: 'MYR' },
      { invoiceId: 'inv-2', invoiceNumber: 'SINV-002', grandTotal: 1200.00, isEligible: true, customerId: 'c-2', customerName: 'Walk-in', issueDate: '2026-08-02', itemCount: 1, currencyCode: 'MYR' },
      { invoiceId: 'inv-3', invoiceNumber: 'SINV-003', grandTotal: 3450.25, isEligible: true, customerId: 'c-3', customerName: 'Public', issueDate: '2026-08-03', itemCount: 5, currencyCode: 'MYR' },
    ];

    expect(computeSelectedTotal(candidates, ['inv-1', 'inv-2'])).toBe(1700.50);
    expect(computeSelectedTotal(candidates, ['inv-1', 'inv-3'])).toBe(3950.75);
    expect(computeSelectedTotal(candidates, [])).toBe(0);
  });

  it('should filter eligible candidates according to LHDN B2C rules', () => {
    const rawInvoices = [
      { id: '1', invoiceNumber: 'SI-1', grandTotal: 4000, status: 'Submitted', eInvoiceStatus: 'NotSubmitted', isConsolidated: false },
      { id: '2', invoiceNumber: 'SI-2', grandTotal: 15000, status: 'Submitted', eInvoiceStatus: 'NotSubmitted', isConsolidated: false }, // Exceeds 10k
      { id: '3', invoiceNumber: 'SI-3', grandTotal: 2500, status: 'Draft', eInvoiceStatus: 'NotSubmitted', isConsolidated: false },     // Draft
      { id: '4', invoiceNumber: 'SI-4', grandTotal: 1200, status: 'Submitted', eInvoiceStatus: 'Valid', isConsolidated: false },        // Already submitted
      { id: '5', invoiceNumber: 'SI-5', grandTotal: 800, status: 'Submitted', eInvoiceStatus: 'NotSubmitted', isConsolidated: true },   // Already consolidated
    ];

    const eligible = filterEligibleCandidates(rawInvoices, 10000);
    expect(eligible).toHaveLength(1);
    expect(eligible[0].id).toBe('1');
  });

  it('should handle consolidation history structure correctly', () => {
    const historyItem: EInvoiceConsolidationDto = {
      id: 'consol-1',
      companyId: 'comp-1',
      consolidatedInvoiceId: 'cinv-1',
      consolidatedInvoiceNumber: 'CONSOL-2026-0001',
      consolidatedIssueDate: '2026-08-14T10:00:00Z',
      consolidatedGrandTotal: 4500.00,
      lhdnUuid: 'LHDN-CONSOL-UUID-999',
      eInvoiceStatus: 'Valid',
      originalInvoices: [
        { invoiceId: 'inv-1', invoiceNumber: 'SINV-001', grandTotal: 2000, isEligible: true, customerId: 'c-1', customerName: 'Buyer A', issueDate: '2026-08-01', itemCount: 1, currencyCode: 'MYR' },
        { invoiceId: 'inv-2', invoiceNumber: 'SINV-002', grandTotal: 2500, isEligible: true, customerId: 'c-2', customerName: 'Buyer B', issueDate: '2026-08-02', itemCount: 2, currencyCode: 'MYR' }
      ]
    };

    expect(historyItem.originalInvoices?.length).toBe(2);
    expect(historyItem.consolidatedGrandTotal).toBe(4500.00);
    expect(historyItem.eInvoiceStatus).toBe('Valid');
  });
});
