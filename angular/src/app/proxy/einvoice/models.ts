import type { EntityDto } from '@abp/ng.core';

export interface CancelEInvoiceDto {
  submissionId: string;
  reason: string;
}

export interface EInvoiceSubmissionDto extends EntityDto<string> {
  companyId?: string;
  submissionUid?: string | null;
  documentUuid?: string | null;
  longId?: string | null;
  sourceDocumentType?: string;
  sourceDocumentId?: string;
  documentTypeCode?: string;
  status?: string;
  reason?: string | null;
  qrCodeUrl?: string | null;
  submittedAt?: string | null;
  validatedAt?: string | null;
  cancelledAt?: string | null;
}

export interface SubmitEInvoiceDto {
  companyId: string;
  sourceDocumentType: string;
  sourceDocumentId: string;
  documentTypeCode?: string;
}
