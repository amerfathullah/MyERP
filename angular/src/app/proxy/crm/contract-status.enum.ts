import { mapEnumToOptions } from '@abp/ng.core';

export enum ContractStatus {
  Unsigned = 0,
  Active = 1,
  InactiveByExpiry = 2,
  InactiveByAutoRenewFailure = 3,
  Cancelled = 4,
}

export const contractStatusOptions = mapEnumToOptions(ContractStatus);
