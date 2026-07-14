import { mapEnumToOptions } from '@abp/ng.core';

export enum PricingRuleType {
  Discount = 0,
  Rate = 1,
  FreeItem = 2,
}

export const pricingRuleTypeOptions = mapEnumToOptions(PricingRuleType);
