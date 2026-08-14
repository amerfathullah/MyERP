import { mapEnumToOptions } from '@abp/ng.core';

export enum AssetCapitalizationStatus {
  Draft = 0,
  Submitted = 1,
  Cancelled = 2,
}

export const assetCapitalizationStatusOptions = mapEnumToOptions(AssetCapitalizationStatus);
