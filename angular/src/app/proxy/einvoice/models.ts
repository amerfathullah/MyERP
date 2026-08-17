import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface BatchSubmitEInvoiceDto {
  companyId: string;
  sourceDocumentType: string;
  documentIds: string[];
}

export interface BatchSubmitItemResult {
  documentId?: string;
  documentNumber?: string;
  success?: boolean;
  errorMessage?: string | null;
  lhdnUuid?: string | null;
  status?: string | null;
}

export interface BatchSubmitResultDto {
  totalRequested?: number;
  succeededCount?: number;
  failedCount?: number;
  skippedCount?: number;
  results?: BatchSubmitItemResult[];
}

export interface CancelEInvoiceDto {
  submissionId: string;
  reason: string;
}

export interface ConsolidateInvoicesDto {
  companyId: string;
  invoiceIds: string[];
}

export interface ConsolidationCandidateDto {
  invoiceId?: string;
  invoiceNumber?: string;
  issueDate?: string;
  customerId?: string;
  customerName?: string;
  grandTotal?: number;
  itemCount?: number;
  currencyCode?: string;
  isEligible?: boolean;
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

export interface EInvoiceConsolidationDto extends EntityDto<string> {
  companyId?: string;
  consolidatedInvoiceId?: string;
  consolidatedInvoiceNumber?: string;
  consolidatedIssueDate?: string;
  consolidatedGrandTotal?: number;
  lhdnUuid?: string | null;
  eInvoiceStatus?: string | null;
  qrCodeUrl?: string | null;
  originalInvoices?: ConsolidationCandidateDto[];
  creationTime?: string;
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

export interface GetConsolidationCandidatesInputDto {
  companyId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  maxAmount?: number | null;
}

export interface GetConsolidationsInputDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface GetLhdnSuccessLogsInputDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  sourceDocumentType?: string | null;
  searchFilter?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface LhdnDashboardStatsDto {
  salesValid?: number;
  salesInvalid?: number;
  salesSubmitted?: number;
  salesCancelled?: number;
  salesFailed?: number;
  salesNotSubmitted?: number;
  purchaseValid?: number;
  purchaseInvalid?: number;
  purchaseSubmitted?: number;
  purchaseCancelled?: number;
  purchaseFailed?: number;
  purchaseNotSubmitted?: number;
}

export interface LhdnStatusReportItemDto {
  invoiceId?: string;
  invoiceNumber?: string;
  postingDate?: string;
  partyName?: string;
  grandTotal?: number;
  taxAmount?: number;
  status?: string;
  documentUuid?: string | null;
  qrCodeUrl?: string | null;
  submittedAt?: string | null;
}

export interface LhdnStatusReportRequestDto {
  companyId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  status?: string | null;
}

export interface LhdnSuccessLogDto extends EntityDto<string> {
  companyId?: string;
  submissionId?: string;
  documentUuid?: string;
  longId?: string | null;
  sourceDocumentType?: string;
  sourceDocumentId?: string;
  sourceDocumentNumber?: string | null;
  documentTypeCode?: string;
  submittedAt?: string;
  validatedAt?: string | null;
  responseJson?: string | null;
  qrCodeUrl?: string | null;
  grandTotal?: number;
  currencyCode?: string;
}

export interface LhdnVatCategorySummaryDto {
  categoryCode?: string;
  categoryName?: string;
  amount?: number;
  adjustment?: number;
  vatAmount?: number;
}

export interface LhdnVatReportDto {
  salesCategories?: LhdnVatCategorySummaryDto[];
  purchaseCategories?: LhdnVatCategorySummaryDto[];
  totalSalesAmount?: number;
  totalSalesAdjustment?: number;
  totalSalesVat?: number;
  totalPurchaseAmount?: number;
  totalPurchaseAdjustment?: number;
  totalPurchaseVat?: number;
  netVatPayable?: number;
}

export interface LhdnVatReportRequestDto {
  companyId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
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

export interface SearchTaxpayerDto {
  idType: string;
  idValue: string;
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
