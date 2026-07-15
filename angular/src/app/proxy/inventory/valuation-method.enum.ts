import { mapEnumToOptions } from '@abp/ng.core';

export enum ValuationMethod {
  FIFO = 0,
  WeightedAverage = 1,
  LIFO = 2,
  StandardCost = 3,
}

export const valuationMethodOptions = mapEnumToOptions(ValuationMethod);
