import { mapEnumToOptions } from '@abp/ng.core';

export enum AssetMaintenanceStatus {
  Planned = 0,
  Completed = 1,
  Cancelled = 2,
  Overdue = 3,
}

export const assetMaintenanceStatusOptions = mapEnumToOptions(AssetMaintenanceStatus);
