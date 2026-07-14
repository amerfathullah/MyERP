import { mapEnumToOptions } from '@abp/ng.core';

export enum BudgetAction {
  Ignore = 0,
  Warn = 1,
  Stop = 2,
}

export const budgetActionOptions = mapEnumToOptions(BudgetAction);
