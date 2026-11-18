import type { BalanceSheetReportDto, BalanceSheetRequestDto, DayBookReportDto, DayBookRequestDto, DimensionWiseAccountsBalanceReportDto, DimensionWiseAccountsBalanceRequestDto, FinancialRatiosReportDto, FinancialRatiosRequestDto, GeneralPaymentLedgerComparisonReportDto, GeneralPaymentLedgerComparisonRequestDto, MonthlyProfitLossReportDto, MonthlyProfitLossRequestDto, PartyTrialBalanceReportDto, PartyTrialBalanceRequestDto, PaymentPeriodBasedOnInvoiceDateReportDto, PaymentPeriodBasedOnInvoiceDateRequestDto, ProfitLossReportDto, ProfitLossRequestDto, ProfitabilityAnalysisReportDto, ProfitabilityAnalysisRequestDto, TrialBalanceReportDto, TrialBalanceRequestDto, VoucherWiseBalanceReportDto, VoucherWiseBalanceRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ReportingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getBalanceSheet = (input: BalanceSheetRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BalanceSheetReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/balance-sheet',
      params: { companyId: input.companyId, asOfDate: input.asOfDate },
    },
      { apiName: this.apiName,...config });
  

  
  getDayBook = (input: DayBookRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DayBookReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/day-book',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, voucherType: input.voucherType, detailed: input.detailed, showRemarks: input.showRemarks, financeBook: input.financeBook, includeDefaultBookEntries: input.includeDefaultBookEntries },
    },
      { apiName: this.apiName,...config });
  

  
  getFinancialRatios = (input: FinancialRatiosRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinancialRatiosReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/financial-ratios',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, includeComparison: input.includeComparison },
    },
      { apiName: this.apiName,...config });
  

  
  getGeneralAndPaymentLedgerComparison = (input: GeneralPaymentLedgerComparisonRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GeneralPaymentLedgerComparisonReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/general-and-payment-ledger-comparison',
      params: { companyId: input.companyId, periodStartDate: input.periodStartDate, periodEndDate: input.periodEndDate, accountId: input.accountId, voucherType: input.voucherType, voucherNo: input.voucherNo, partyType: input.partyType, partyId: input.partyId },
    },
      { apiName: this.apiName,...config });
  

  
  getMonthlyProfitLoss = (input: MonthlyProfitLossRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MonthlyProfitLossReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/monthly-profit-loss',
      params: { companyId: input.companyId, year: input.year, startMonth: input.startMonth },
    },
      { apiName: this.apiName,...config });
  

  
  getProfitLoss = (input: ProfitLossRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfitLossReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/profit-loss',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, includeComparison: input.includeComparison, financeBook: input.financeBook, includeDefaultBookEntries: input.includeDefaultBookEntries },
    },
      { apiName: this.apiName,...config });
  

  
  getProfitabilityAnalysis = (input: ProfitabilityAnalysisRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfitabilityAnalysisReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/profitability-analysis',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, basedOn: input.basedOn, financeBook: input.financeBook, includeDefaultBookEntries: input.includeDefaultBookEntries, showZeroValues: input.showZeroValues },
    },
      { apiName: this.apiName,...config });
  

  
  getTrialBalance = (input: TrialBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TrialBalanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/trial-balance',
      params: { companyId: input.companyId, asOfDate: input.asOfDate, fiscalYearId: input.fiscalYearId, includeSubsidiaries: input.includeSubsidiaries },
    },
      { apiName: this.apiName,...config });
  

  
  getTrialBalanceForParty = (input: PartyTrialBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyTrialBalanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/trial-balance-for-party',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, partyType: input.partyType, partyId: input.partyId, accountId: input.accountId, excludeZeroBalanceParties: input.excludeZeroBalanceParties, showZeroValues: input.showZeroValues },
    },
      { apiName: this.apiName,...config });

  
  getVoucherWiseBalance = (input: VoucherWiseBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VoucherWiseBalanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/voucher-wise-balance',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, voucherType: input.voucherType },
    },
      { apiName: this.apiName,...config });

  
  getPaymentPeriodBasedOnInvoiceDate = (input: PaymentPeriodBasedOnInvoiceDateRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentPeriodBasedOnInvoiceDateReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/payment-period-based-on-invoice-date',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, paymentType: input.paymentType, partyType: input.partyType, partyId: input.partyId },
    },
      { apiName: this.apiName,...config });

  
  getDimensionWiseAccountsBalance = (input: DimensionWiseAccountsBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DimensionWiseAccountsBalanceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/reporting/dimension-wise-accounts-balance',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, dimension: input.dimension, financeBook: input.financeBook, includeDefaultBookEntries: input.includeDefaultBookEntries, showZeroValues: input.showZeroValues },
    },
      { apiName: this.apiName,...config });
}
