import { mapEnumToOptions } from '@abp/ng.core';

export enum WorkOrderStatus {
  Draft = 0,
  Submitted = 1,
  NotStarted = 2,
  InProcess = 3,
  Completed = 4,
  Stopped = 5,
  Cancelled = 6,
}

export const workOrderStatusOptions = mapEnumToOptions(WorkOrderStatus);
