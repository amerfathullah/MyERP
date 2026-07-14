import { mapEnumToOptions } from '@abp/ng.core';

export enum LeaveApplicationStatus {
  Open = 0,
  Approved = 1,
  Rejected = 2,
  Cancelled = 3,
}

export const leaveApplicationStatusOptions = mapEnumToOptions(LeaveApplicationStatus);
