import { mapEnumToOptions } from '@abp/ng.core';

export enum ShiftAssignmentStatus {
  Active = 0,
  Inactive = 1,
}

export const shiftAssignmentStatusOptions = mapEnumToOptions(ShiftAssignmentStatus);
