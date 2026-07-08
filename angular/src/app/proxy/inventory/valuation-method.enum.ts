import { mapEnumToOptions } from '@abp/ng.core';

export enum ValuationMethod {
  FIFO = 0,
  WeightedAverage = 1,
}

export const valuationMethodOptions = mapEnumToOptions(ValuationMethod);
