import { mapEnumToOptions } from '@abp/ng.core';

export enum ProjectTaskStatus {
  Open = 0,
  Working = 1,
  PendingReview = 2,
  Overdue = 3,
  Completed = 4,
  Cancelled = 5,
}

export const projectTaskStatusOptions = mapEnumToOptions(ProjectTaskStatus);
