import { mapEnumToOptions } from '@abp/ng.core';

export enum QualityFeedbackDocumentType {
  User = 0,
  Customer = 1,
}

export const qualityFeedbackDocumentTypeOptions = mapEnumToOptions(QualityFeedbackDocumentType);
