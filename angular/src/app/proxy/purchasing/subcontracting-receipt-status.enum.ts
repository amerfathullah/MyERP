import { mapEnumToOptions } from '@abp/ng.core';

export enum SubcontractingReceiptStatus {
  Draft = 0,
  Submitted = 1,
  Cancelled = 2,
}

export const subcontractingReceiptStatusOptions = mapEnumToOptions(SubcontractingReceiptStatus);
