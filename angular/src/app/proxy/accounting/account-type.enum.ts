import { mapEnumToOptions } from '@abp/ng.core';

export enum AccountType {
  Asset = 0,
  Liability = 1,
  Equity = 2,
  Revenue = 3,
  Expense = 4,
}

export const accountTypeOptions = mapEnumToOptions(AccountType);
