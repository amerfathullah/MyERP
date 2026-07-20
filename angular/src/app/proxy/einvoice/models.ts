import type { EntityDto } from '@abp/ng.core';

export interface CancelEInvoiceDto {
  submissionId: string;
  reason: string;
}

export interface EInvoiceConnectResultDto {
  isSuccess?: boolean;
  errorMessage?: string | null;
  tokenExpiresAt?: string | null;
}

export interface EInvoiceConnectionStatusDto {
  isConfigured?: boolean;
  isConnected?: boolean;
  isTokenExpired?: boolean;
  environment?: string;
  clientId?: string | null;
  tokenExpiresAt?: string | null;
  isCertificateConfigured?: boolean;
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

export interface SaveEInvoiceCertificateDto {
  certificateBase64: string;
  certificatePassword?: string | null;
}

export interface SaveEInvoiceCredentialsDto {
  clientId: string;
  clientSecret?: string | null;
  environment: string;
}

export interface SubmitEInvoiceDto {
  companyId: string;
  sourceDocumentType: string;
  sourceDocumentId: string;
  documentTypeCode?: string;
}

export interface TaxpayerSearchResultDto {
  isSuccess?: boolean;
  errorMessage?: string | null;
  tin?: string | null;
  name?: string | null;
  idType?: string | null;
  idValue?: string | null;
}
