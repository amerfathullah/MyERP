import { mapEnumToOptions } from '@abp/ng.core';

export enum SecondaryItemType {
  CoProduct = 0,
  ByProduct = 1,
  Scrap = 2,
  AdditionalFinishedGood = 3,
}

export const secondaryItemTypeOptions = mapEnumToOptions(SecondaryItemType);
