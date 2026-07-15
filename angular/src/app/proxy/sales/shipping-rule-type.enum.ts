import { mapEnumToOptions } from '@abp/ng.core';

export enum ShippingRuleType {
  Selling = 0,
  Buying = 1,
}

export const shippingRuleTypeOptions = mapEnumToOptions(ShippingRuleType);
