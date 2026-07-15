import { mapEnumToOptions } from '@abp/ng.core';

export enum ScorecardPeriodType {
  Weekly = 0,
  Monthly = 1,
  Yearly = 2,
}

export const scorecardPeriodTypeOptions = mapEnumToOptions(ScorecardPeriodType);
