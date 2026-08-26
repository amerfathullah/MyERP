import { mapEnumToOptions } from '@abp/ng.core';

export enum CallStatus {
  Ringing = 0,
  InProgress = 1,
  Completed = 2,
  Failed = 3,
  Busy = 4,
  NoAnswer = 5,
  Queued = 6,
  Cancelled = 7,
}

export const callStatusOptions = mapEnumToOptions(CallStatus);
