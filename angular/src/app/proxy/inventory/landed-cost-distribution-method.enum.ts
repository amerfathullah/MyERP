import { mapEnumToOptions } from '@abp/ng.core';

export enum LandedCostDistributionMethod {
  BasedOnQuantity = 0,
  BasedOnAmount = 1,
  Manual = 2,
}

export const landedCostDistributionMethodOptions = mapEnumToOptions(LandedCostDistributionMethod);
