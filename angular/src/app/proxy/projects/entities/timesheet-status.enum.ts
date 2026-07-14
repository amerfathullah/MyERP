import { mapEnumToOptions } from '@abp/ng.core';

export enum TimesheetStatus {
  Draft = 0,
  Submitted = 1,
  Billed = 2,
  Cancelled = 3,
}

export const timesheetStatusOptions = mapEnumToOptions(TimesheetStatus);
