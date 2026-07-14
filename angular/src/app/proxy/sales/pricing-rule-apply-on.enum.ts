import { mapEnumToOptions } from '@abp/ng.core';

export enum PricingRuleApplyOn {
  ItemCode = 0,
  ItemGroup = 1,
  Brand = 2,
  TransactionTotal = 3,
}

export const pricingRuleApplyOnOptions = mapEnumToOptions(PricingRuleApplyOn);
