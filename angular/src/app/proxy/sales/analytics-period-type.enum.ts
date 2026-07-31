import { mapEnumToOptions } from '@abp/ng.core';

export enum AnalyticsPeriodType {
  Monthly = 0,
  Quarterly = 1,
  Yearly = 2,
}

export const analyticsPeriodTypeOptions = mapEnumToOptions(AnalyticsPeriodType);
