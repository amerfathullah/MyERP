import { mapEnumToOptions } from '@abp/ng.core';

export enum AssetMovementPurpose {
  Issue = 0,
  Receipt = 1,
  Transfer = 2,
  TransferAndIssue = 3,
}

export const assetMovementPurposeOptions = mapEnumToOptions(AssetMovementPurpose);
