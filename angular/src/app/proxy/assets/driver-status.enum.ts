import { mapEnumToOptions } from '@abp/ng.core';

export enum DriverStatus {
  Active = 0,
  Suspended = 1,
  Left = 2,
}

export const driverStatusOptions = mapEnumToOptions(DriverStatus);
