import { mapEnumToOptions } from '@abp/ng.core';

export enum DeferredAccountingType {
  Income = 1,
  Expense = 2,
}

export const deferredAccountingTypeOptions = mapEnumToOptions(DeferredAccountingType);
