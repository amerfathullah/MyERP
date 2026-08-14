import { mapEnumToOptions } from '@abp/ng.core';

export enum QualityReviewStatus {
  Open = 0,
  Passed = 1,
  Failed = 2,
}

export const qualityReviewStatusOptions = mapEnumToOptions(QualityReviewStatus);
