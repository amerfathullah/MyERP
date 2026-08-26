import { mapEnumToOptions } from '@abp/ng.core';

export enum EmploymentStatus {
  Active = 0,
  Probation = 1,
  OnLeave = 2,
  Resigned = 3,
  Terminated = 4,
}

export const employmentStatusOptions = mapEnumToOptions(EmploymentStatus);
