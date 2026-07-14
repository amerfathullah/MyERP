import { mapEnumToOptions } from '@abp/ng.core';

export enum AssetStatus {
  Draft = 0,
  Submitted = 1,
  PartiallyDepreciated = 2,
  FullyDepreciated = 3,
  Sold = 4,
  Scrapped = 5,
  InMaintenance = 6,
  Cancelled = 7,
}

export const assetStatusOptions = mapEnumToOptions(AssetStatus);
