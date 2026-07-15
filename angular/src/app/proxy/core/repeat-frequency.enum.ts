import { mapEnumToOptions } from '@abp/ng.core';

export enum RepeatFrequency {
  Daily = 0,
  Weekly = 1,
  Monthly = 2,
  Quarterly = 3,
  HalfYearly = 4,
  Yearly = 5,
}

export const repeatFrequencyOptions = mapEnumToOptions(RepeatFrequency);
