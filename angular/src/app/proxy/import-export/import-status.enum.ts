import { mapEnumToOptions } from '@abp/ng.core';

export enum ImportStatus {
  Pending = 0,
  Processing = 1,
  Completed = 2,
  Failed = 3,
  PartialSuccess = 4,
}

export const importStatusOptions = mapEnumToOptions(ImportStatus);
