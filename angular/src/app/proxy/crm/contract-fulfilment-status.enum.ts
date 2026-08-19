import { mapEnumToOptions } from '@abp/ng.core';

export enum ContractFulfilmentStatus {
  NotApplicable = 0,
  Unfulfilled = 1,
  PartiallyFulfilled = 2,
  Fulfilled = 3,
  Lapsed = 4,
}

export const contractFulfilmentStatusOptions = mapEnumToOptions(ContractFulfilmentStatus);
