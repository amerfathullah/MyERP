import { mapEnumToOptions } from '@abp/ng.core';

export enum AssetActivityType {
  Created = 0,
  Depreciated = 1,
  Moved = 2,
  Repaired = 3,
  Capitalized = 4,
  Adjusted = 5,
  Scrapped = 6,
  Sold = 7,
}

export const assetActivityTypeOptions = mapEnumToOptions(AssetActivityType);
