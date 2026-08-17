import { mapEnumToOptions } from '@abp/ng.core';

export enum AppointmentStatus {
  Unverified = 0,
  Open = 1,
  Closed = 2,
}

export const appointmentStatusOptions = mapEnumToOptions(AppointmentStatus);
