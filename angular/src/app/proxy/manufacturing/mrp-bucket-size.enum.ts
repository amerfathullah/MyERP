import { mapEnumToOptions } from '@abp/ng.core';

export enum MrpBucketSize {
  Daily = 0,
  Weekly = 1,
  Monthly = 2,
}

export const mrpBucketSizeOptions = mapEnumToOptions(MrpBucketSize);
