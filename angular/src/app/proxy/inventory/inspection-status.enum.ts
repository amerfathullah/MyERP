import { mapEnumToOptions } from '@abp/ng.core';

export enum InspectionStatus {
  Draft = 0,
  Accepted = 1,
  Rejected = 2,
}

export const inspectionStatusOptions = mapEnumToOptions(InspectionStatus);
