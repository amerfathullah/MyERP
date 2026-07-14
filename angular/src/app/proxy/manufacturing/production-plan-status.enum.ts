import { mapEnumToOptions } from '@abp/ng.core';

export enum ProductionPlanStatus {
  Draft = 0,
  Submitted = 1,
  InProgress = 2,
  Completed = 3,
  Cancelled = 4,
}

export const productionPlanStatusOptions = mapEnumToOptions(ProductionPlanStatus);
