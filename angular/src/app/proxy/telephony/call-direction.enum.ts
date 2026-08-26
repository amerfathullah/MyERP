import { mapEnumToOptions } from '@abp/ng.core';

export enum CallDirection {
  Incoming = 0,
  Outgoing = 1,
}

export const callDirectionOptions = mapEnumToOptions(CallDirection);
