import { mapEnumToOptions } from '@abp/ng.core';

export enum ProjectStatus {
  Open = 0,
  Completed = 1,
  Cancelled = 2,
}

export const projectStatusOptions = mapEnumToOptions(ProjectStatus);
