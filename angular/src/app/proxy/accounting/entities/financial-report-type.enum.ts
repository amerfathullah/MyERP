import { mapEnumToOptions } from '@abp/ng.core';

export enum FinancialReportType {
  ProfitAndLoss = 0,
  BalanceSheet = 1,
  CashFlow = 2,
  Custom = 3,
}

export const financialReportTypeOptions = mapEnumToOptions(FinancialReportType);
