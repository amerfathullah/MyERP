import { mapEnumToOptions } from '@abp/ng.core';

export enum PercentCompleteMethod {
  Manual = 0,
  TaskCompletion = 1,
  TaskProgress = 2,
  TaskWeight = 3,
}

export const percentCompleteMethodOptions = mapEnumToOptions(PercentCompleteMethod);
