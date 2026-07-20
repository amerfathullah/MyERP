import type { AutoMatchResultDto, BankReconciliationSummaryDto, BankTransactionDto, CreateInternalTransferDto, GetBankTransactionsDto, ImportBankTransactionDto, InternalTransferResultDto, MatchCandidateDto, MirrorTransactionDto, ReconcileBankTransactionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  autoMatch = (bankAccountId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutoMatchResultDto>({
      method: 'POST',
      url: '/api/app/bank-reconciliation/auto-match',
      params: { bankAccountId, companyId },
    },
    { apiName: this.apiName,...config });
  

  createInternalTransfer = (input: CreateInternalTransferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InternalTransferResultDto>({
      method: 'POST',
      url: '/api/app/bank-reconciliation/internal-transfer',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getMatchCandidates = (bankTransactionId: string, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MatchCandidateDto[]>({
      method: 'GET',
      url: '/api/app/bank-reconciliation/match-candidates',
      params: { bankTransactionId, companyId },
    },
    { apiName: this.apiName,...config });
  

  getSummary = (bankAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankReconciliationSummaryDto>({
      method: 'GET',
      url: `/api/app/bank-reconciliation/summary/${bankAccountId}`,
    },
    { apiName: this.apiName,...config });
  

  getTransactions = (input: GetBankTransactionsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankTransactionDto>>({
      method: 'GET',
      url: '/api/app/bank-reconciliation/transactions',
      params: { bankAccountId: input.bankAccountId, isReconciled: input.isReconciled, dateFrom: input.dateFrom, dateTo: input.dateTo, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  importTransaction = (input: ImportBankTransactionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankTransactionDto>({
      method: 'POST',
      url: '/api/app/bank-reconciliation/import-transaction',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  reconcile = (input: ReconcileBankTransactionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankTransactionDto>({
      method: 'POST',
      url: '/api/app/bank-reconciliation/reconcile',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  searchForMirrorTransaction = (transactionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MirrorTransactionDto>({
      method: 'POST',
      url: `/api/app/bank-reconciliation/search-for-mirror-transaction/${transactionId}`,
    },
    { apiName: this.apiName,...config });
  

  unreconcile = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankTransactionDto>({
      method: 'POST',
      url: `/api/app/bank-reconciliation/${id}/unreconcile`,
    },
    { apiName: this.apiName,...config });
}