import { mapEnumToOptions } from '@abp/ng.core';

export enum CouponType {
  Promotional = 0,
  GiftCard = 1,
}

export const couponTypeOptions = mapEnumToOptions(CouponType);
