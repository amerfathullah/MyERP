import { mapEnumToOptions } from '@abp/ng.core';

export enum SubcontractingInwardOrderStatus {
  Draft = 0,
  Open = 1,
  PartiallyReceived = 2,
  Completed = 3,
  Closed = 4,
  Cancelled = 5,
}

export const subcontractingInwardOrderStatusOptions = mapEnumToOptions(SubcontractingInwardOrderStatus);
