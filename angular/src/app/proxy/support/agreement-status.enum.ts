import { mapEnumToOptions } from '@abp/ng.core';

export enum AgreementStatus {
  FirstResponseDue = 0,
  ResolutionDue = 1,
  Fulfilled = 2,
  Failed = 3,
  Paused = 4,
}

export const agreementStatusOptions = mapEnumToOptions(AgreementStatus);
