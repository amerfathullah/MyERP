import { mapEnumToOptions } from '@abp/ng.core';

export enum DocumentStatus {
  Draft = 0,
  Submitted = 1,
  Approved = 2,
  Posted = 3,
  Cancelled = 4,
  Rejected = 5,
  ToDeliverAndBill = 10,
  ToDeliver = 11,
  ToBill = 12,
  Completed = 13,
  Closed = 14,
}

export const documentStatusOptions = mapEnumToOptions(DocumentStatus);
