import type { BalanceSheetReportDto, BalanceSheetRequestDto, MonthlyProfitLossReportDto, MonthlyProfitLossRequestDto, PartyTrialBalanceReportDto, PartyTrialBalanceRequestDto, ProfitLossReportDto, ProfitLossRequestDto, TrialBalanceReportDto, TrialBalanceRequestDto } from './models';
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
      url: '/api/app/reporting/balance-sheet',
      params: { companyId: input.companyId, asOfDate: input.asOfDate },
    },
    { apiName: this.apiName,...config });
  

  getMonthlyProfitLoss = (input: MonthlyProfitLossRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MonthlyProfitLossReportDto>({
      method: 'GET',
      url: '/api/app/reporting/monthly-profit-loss',
      params: { companyId: input.companyId, year: input.year, startMonth: input.startMonth },
    },
    { apiName: this.apiName,...config });
  

  getProfitLoss = (input: ProfitLossRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfitLossReportDto>({
      method: 'GET',
      url: '/api/app/reporting/profit-loss',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, includeComparison: input.includeComparison },
    },
    { apiName: this.apiName,...config });
  

  getTrialBalance = (input: TrialBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TrialBalanceReportDto>({
      method: 'GET',
      url: '/api/app/reporting/trial-balance',
      params: { companyId: input.companyId, asOfDate: input.asOfDate, fiscalYearId: input.fiscalYearId, includeSubsidiaries: input.includeSubsidiaries },
    },
    { apiName: this.apiName,...config });
  

  getTrialBalanceForParty = (input: PartyTrialBalanceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyTrialBalanceReportDto>({
      method: 'GET',
      url: '/api/app/reporting/trial-balance-for-party',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, partyType: input.partyType, partyId: input.partyId, accountId: input.accountId, excludeZeroBalanceParties: input.excludeZeroBalanceParties, showZeroValues: input.showZeroValues },
    },
    { apiName: this.apiName,...config });
}