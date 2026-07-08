import { mapEnumToOptions } from '@abp/ng.core';

export enum ItemType {
  Goods = 0,
  Service = 1,
  FixedAsset = 2,
}

export const itemTypeOptions = mapEnumToOptions(ItemType);
