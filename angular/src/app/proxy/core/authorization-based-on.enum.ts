import { mapEnumToOptions } from '@abp/ng.core';

export enum AuthorizationBasedOn {
  GrandTotal = 0,
  AverageDiscount = 1,
  CustomerwiseDiscount = 2,
  ItemwiseDiscount = 3,
  ItemGroupWiseDiscount = 4,
}

export const authorizationBasedOnOptions = mapEnumToOptions(AuthorizationBasedOn);
