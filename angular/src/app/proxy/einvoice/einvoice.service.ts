import type { BatchSubmitEInvoiceDto, BatchSubmitResultDto, CancelEInvoiceDto, ConsolidateInvoicesDto, ConsolidationCandidateDto, EInvoiceConsolidationDto, EInvoiceSubmissionDto, GetConsolidationCandidatesInputDto, GetConsolidationsInputDto, GetLhdnSuccessLogsInputDto, LhdnDashboardStatsDto, LhdnStatusReportItemDto, LhdnStatusReportRequestDto, LhdnSuccessLogDto, LhdnVatReportDto, LhdnVatReportRequestDto, SearchTaxpayerDto, SubmitEInvoiceDto, TaxpayerSearchResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EInvoiceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  batchSubmit = (input: BatchSubmitEInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchSubmitResultDto>({
      method: 'POST',
      url: '/api/app/e-invoice/batch-submit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  cancel = (input: CancelEInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'POST',
      url: '/api/app/e-invoice/cancel',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  consolidateInvoices = (input: ConsolidateInvoicesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'POST',
      url: '/api/app/e-invoice/consolidate-invoices',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getConsolidationCandidates = (input: GetConsolidationCandidatesInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ConsolidationCandidateDto[]>({
      method: 'GET',
      url: '/api/app/e-invoice/consolidation-candidates',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, maxAmount: input.maxAmount },
    },
    { apiName: this.apiName,...config });
  

  getConsolidations = (input: GetConsolidationsInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EInvoiceConsolidationDto>>({
      method: 'GET',
      url: '/api/app/e-invoice/consolidations',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getDashboardStats = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LhdnDashboardStatsDto>({
      method: 'GET',
      url: `/api/app/e-invoice/dashboard-stats/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EInvoiceSubmissionDto>>({
      method: 'GET',
      url: '/api/app/e-invoice',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPurchaseStatusReport = (input: LhdnStatusReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LhdnStatusReportItemDto[]>({
      method: 'GET',
      url: '/api/app/e-invoice/purchase-status-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, status: input.status },
    },
    { apiName: this.apiName,...config });
  

  getSalesStatusReport = (input: LhdnStatusReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LhdnStatusReportItemDto[]>({
      method: 'GET',
      url: '/api/app/e-invoice/sales-status-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, status: input.status },
    },
    { apiName: this.apiName,...config });
  

  getStatus = (submissionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'GET',
      url: `/api/app/e-invoice/status/${submissionId}`,
    },
    { apiName: this.apiName,...config });
  

  getSuccessLogs = (input: GetLhdnSuccessLogsInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LhdnSuccessLogDto>>({
      method: 'GET',
      url: '/api/app/e-invoice/success-logs',
      params: { companyId: input.companyId, sourceDocumentType: input.sourceDocumentType, searchFilter: input.searchFilter, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getVatReport = (input: LhdnVatReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LhdnVatReportDto>({
      method: 'GET',
      url: '/api/app/e-invoice/vat-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
  

  refreshStatus = (submissionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'POST',
      url: `/api/app/e-invoice/refresh-status/${submissionId}`,
    },
    { apiName: this.apiName,...config });
  

  searchTaxpayer = (input: SearchTaxpayerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxpayerSearchResultDto>({
      method: 'POST',
      url: '/api/app/e-invoice/search-taxpayer',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submit = (input: SubmitEInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'POST',
      url: '/api/app/e-invoice/submit',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitConsolidated = (input: SubmitEInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'POST',
      url: '/api/app/e-invoice/submit-consolidated',
      body: input,
    },
    { apiName: this.apiName,...config });
}