import { mapEnumToOptions } from '@abp/ng.core';

export enum EmailDigestFrequency {
  Daily = 0,
  Weekly = 1,
  Monthly = 2,
}

export const emailDigestFrequencyOptions = mapEnumToOptions(EmailDigestFrequency);
