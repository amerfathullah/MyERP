import { mapEnumToOptions } from '@abp/ng.core';

export enum SerialNoStatus {
  Active = 0,
  Delivered = 1,
  Consumed = 2,
  Inactive = 3,
}

export const serialNoStatusOptions = mapEnumToOptions(SerialNoStatus);
