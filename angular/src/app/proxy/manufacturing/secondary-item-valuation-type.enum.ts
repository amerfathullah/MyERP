import { mapEnumToOptions } from '@abp/ng.core';

export enum SecondaryItemValuationType {
  ValuationRate = 0,
  PercentageOfComponentCost = 1,
  PercentageOfFgCost = 1,
  Manual = 2,
}

export const secondaryItemValuationTypeOptions = mapEnumToOptions(SecondaryItemValuationType);
