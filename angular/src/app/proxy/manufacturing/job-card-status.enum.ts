import { mapEnumToOptions } from '@abp/ng.core';

export enum JobCardStatus {
  Open = 0,
  WorkInProgress = 1,
  MaterialTransferred = 2,
  Completed = 3,
  OnHold = 4,
  Cancelled = 5,
}

export const jobCardStatusOptions = mapEnumToOptions(JobCardStatus);
