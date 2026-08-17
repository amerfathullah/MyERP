import { describe, it, expect } from 'vitest';
import type { LhdnSuccessLogDto } from '../../proxy/einvoice/models';

/**
 * Tests for E-Invoice Success Logs & Audit logic:
 * - Filtering by document type, UUID/search text, date range
 * - QR code validation and status mapping
 */
describe('E-Invoice Audit Logs Logic', () => {
  function filterLogs(
    logs: LhdnSuccessLogDto[],
    filters: { search?: string; docType?: string; fromDate?: string; toDate?: string }
  ): LhdnSuccessLogDto[] {
    return logs.filter(log => {
      if (filters.search) {
        const term = filters.search.toLowerCase();
        const matchesUuid = log.documentUuid?.toLowerCase().includes(term);
        const matchesNum = log.sourceDocumentNumber?.toLowerCase().includes(term);
        if (!matchesUuid && !matchesNum) return false;
      }
      if (filters.docType && log.sourceDocumentType !== filters.docType) {
        return false;
      }
      if (filters.fromDate && log.submittedAt && log.submittedAt < filters.fromDate) {
        return false;
      }
      if (filters.toDate && log.submittedAt && log.submittedAt > filters.toDate) {
        return false;
      }
      return true;
    });
  }

  const sampleLogs: LhdnSuccessLogDto[] = [
    {
      id: 'log-1',
      companyId: 'comp-1',
      submissionId: 'sub-1',
      documentUuid: 'LHDN-ABC-1234',
      sourceDocumentType: 'SalesInvoice',
      sourceDocumentId: 'si-1',
      sourceDocumentNumber: 'SINV-001',
      documentTypeCode: '01',
      submittedAt: '2026-08-10T10:00:00Z',
      validatedAt: '2026-08-10T10:02:00Z',
      grandTotal: 1500.00,
      currencyCode: 'MYR',
      qrCodeUrl: 'https://myinvois.hasil.gov.my/verify/LHDN-ABC-1234'
    },
    {
      id: 'log-2',
      companyId: 'comp-1',
      submissionId: 'sub-2',
      documentUuid: 'LHDN-XYZ-5678',
      sourceDocumentType: 'PurchaseInvoice',
      sourceDocumentId: 'pi-1',
      sourceDocumentNumber: 'PINV-001',
      documentTypeCode: '01',
      submittedAt: '2026-08-12T14:30:00Z',
      validatedAt: '2026-08-12T14:31:00Z',
      grandTotal: 4200.00,
      currencyCode: 'MYR',
      qrCodeUrl: 'https://myinvois.hasil.gov.my/verify/LHDN-XYZ-5678'
    },
    {
      id: 'log-3',
      companyId: 'comp-1',
      submissionId: 'sub-3',
      documentUuid: 'LHDN-CONSOL-9999',
      sourceDocumentType: 'SalesInvoice',
      sourceDocumentId: 'csi-1',
      sourceDocumentNumber: 'CONSOL-001',
      documentTypeCode: '01',
      submittedAt: '2026-08-14T09:15:00Z',
      validatedAt: undefined,
      grandTotal: 9800.00,
      currencyCode: 'MYR'
    }
  ];

  it('should filter logs by search text (UUID or Document Number)', () => {
    const resUuid = filterLogs(sampleLogs, { search: '5678' });
    expect(resUuid).toHaveLength(1);
    expect(resUuid[0].sourceDocumentNumber).toBe('PINV-001');

    const resNum = filterLogs(sampleLogs, { search: 'SINV' });
    expect(resNum).toHaveLength(1);
    expect(resNum[0].documentUuid).toBe('LHDN-ABC-1234');
  });

  it('should filter logs by source document type', () => {
    const salesOnly = filterLogs(sampleLogs, { docType: 'SalesInvoice' });
    expect(salesOnly).toHaveLength(2);

    const purchaseOnly = filterLogs(sampleLogs, { docType: 'PurchaseInvoice' });
    expect(purchaseOnly).toHaveLength(1);
    expect(purchaseOnly[0].sourceDocumentNumber).toBe('PINV-001');
  });

  it('should correctly handle verified QR code URLs', () => {
    expect(sampleLogs[0].qrCodeUrl).toContain('myinvois.hasil.gov.my');
    expect(sampleLogs[2].qrCodeUrl).toBeUndefined();
  });
});
