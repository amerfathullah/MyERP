import { mapEnumToOptions } from '@abp/ng.core';

export enum ShippingCalculationMode {
  Fixed = 0,
  BasedOnNetTotal = 1,
  BasedOnNetWeight = 2,
}

export const shippingCalculationModeOptions = mapEnumToOptions(ShippingCalculationMode);
