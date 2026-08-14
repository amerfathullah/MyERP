import { mapEnumToOptions } from '@abp/ng.core';

export enum QualityActionType {
  Corrective = 0,
  Preventive = 1,
}

export const qualityActionTypeOptions = mapEnumToOptions(QualityActionType);
