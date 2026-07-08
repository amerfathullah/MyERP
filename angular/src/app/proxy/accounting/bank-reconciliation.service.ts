import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface BankTransactionDto {
  id?: string;
  companyId?: string;
  bankAccountId?: string;
  transactionDate?: string;
  description?: string;
  amount?: number;
  referenceNumber?: string;
  isReconciled?: boolean;
  paymentEntryId?: string;
  matchedDocumentRef?: string;
  reconciledAt?: string;
}

export interface GetBankTransactionsDto {
  bankAccountId: string;
  isReconciled?: boolean;
  dateFrom?: string;
  dateTo?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

export interface ReconcileBankTransactionDto {
  transactionId: string;
  paymentEntryId: string;
  matchedDocumentRef?: string;
}

export interface BankReconciliationSummaryDto {
  totalTransactions?: number;
  reconciledCount?: number;
  unreconciledCount?: number;
  totalDeposits?: number;
  totalWithdrawals?: number;
  unreconciledBalance?: number;
}

@Injectable({ providedIn: 'root' })
export class BankReconciliationService {
  private restService = inject(RestService);
  apiName = 'Default';

  getTransactions = (input: GetBankTransactionsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BankTransactionDto>>({
      method: 'GET',
      url: '/api/app/bank-reconciliation/transactions',
      params: { ...input },
    }, { apiName: this.apiName, ...config });

  reconcile = (input: ReconcileBankTransactionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankTransactionDto>({
      method: 'POST',
      url: '/api/app/bank-reconciliation/reconcile',
      body: input,
    }, { apiName: this.apiName, ...config });

  unreconcile = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankTransactionDto>({
      method: 'POST',
      url: `/api/app/bank-reconciliation/unreconcile/${id}`,
    }, { apiName: this.apiName, ...config });

  getSummary = (bankAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankReconciliationSummaryDto>({
      method: 'GET',
      url: `/api/app/bank-reconciliation/summary/${bankAccountId}`,
    }, { apiName: this.apiName, ...config });
}
