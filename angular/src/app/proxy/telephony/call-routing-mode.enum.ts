import { mapEnumToOptions } from '@abp/ng.core';

export enum CallRoutingMode {
  Sequential = 0,
  Simultaneous = 1,
}

export const callRoutingModeOptions = mapEnumToOptions(CallRoutingMode);
