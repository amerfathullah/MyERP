import { mapEnumToOptions } from '@abp/ng.core';

export enum UnreconcileVoucherType {
  PaymentEntry = 0,
  JournalEntry = 1,
}

export const unreconcileVoucherTypeOptions = mapEnumToOptions(UnreconcileVoucherType);
