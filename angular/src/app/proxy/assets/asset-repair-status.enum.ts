import { mapEnumToOptions } from '@abp/ng.core';

export enum AssetRepairStatus {
  Pending = 0,
  Completed = 1,
  Cancelled = 2,
}

export const assetRepairStatusOptions = mapEnumToOptions(AssetRepairStatus);
