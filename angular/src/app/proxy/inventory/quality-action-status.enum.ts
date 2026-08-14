import { mapEnumToOptions } from '@abp/ng.core';

export enum QualityActionStatus {
  Open = 0,
  Resolved = 1,
  Closed = 2,
}

export const qualityActionStatusOptions = mapEnumToOptions(QualityActionStatus);
