import { mapEnumToOptions } from '@abp/ng.core';

export enum AttendanceStatus {
  Present = 0,
  Absent = 1,
  HalfDay = 2,
  OnLeave = 3,
}

export const attendanceStatusOptions = mapEnumToOptions(AttendanceStatus);
