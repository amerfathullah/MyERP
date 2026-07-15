import { mapEnumToOptions } from '@abp/ng.core';

export enum AccountSubType {
  CurrentAsset = 10,
  FixedAsset = 11,
  BankAccount = 12,
  CashAccount = 13,
  AccountsReceivable = 14,
  Stock = 15,
  AccumulatedDepreciation = 16,
  CapitalWorkInProgress = 17,
  CurrentLiability = 20,
  LongTermLiability = 21,
  AccountsPayable = 22,
  TaxPayable = 23,
  ShareCapital = 30,
  RetainedEarnings = 31,
  TemporaryOpening = 32,
  OperatingRevenue = 40,
  OtherIncome = 41,
  OperatingExpense = 50,
  CostOfGoodsSold = 51,
  DepreciationExpense = 52,
  TaxExpense = 53,
}

export const accountSubTypeOptions = mapEnumToOptions(AccountSubType);
