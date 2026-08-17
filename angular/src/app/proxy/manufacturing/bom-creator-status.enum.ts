import { mapEnumToOptions } from '@abp/ng.core';

export enum BomCreatorStatus {
  Draft = 0,
  Completed = 1,
  Failed = 2,
}

export const bomCreatorStatusOptions = mapEnumToOptions(BomCreatorStatus);
