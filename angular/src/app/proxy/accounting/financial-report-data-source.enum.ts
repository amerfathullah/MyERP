import { mapEnumToOptions } from '@abp/ng.core';

export enum FinancialReportDataSource {
  AccountData = 0,
  CalculatedAmount = 1,
  CustomApi = 2,
  BlankLine = 3,
  ColumnBreak = 4,
  SectionBreak = 5,
}

export const financialReportDataSourceOptions = mapEnumToOptions(FinancialReportDataSource);
