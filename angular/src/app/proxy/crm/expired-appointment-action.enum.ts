import { mapEnumToOptions } from '@abp/ng.core';

export enum ExpiredAppointmentAction {
  NoAction = 0,
  CancelAppointment = 1,
}

export const expiredAppointmentActionOptions = mapEnumToOptions(ExpiredAppointmentAction);
