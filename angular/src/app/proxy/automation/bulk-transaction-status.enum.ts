import { mapEnumToOptions } from '@abp/ng.core';

export enum BulkTransactionStatus {
  Queued = 0,
  InProgress = 1,
  Success = 2,
  Failed = 3,
  Retried = 4,
}

export const bulkTransactionStatusOptions = mapEnumToOptions(BulkTransactionStatus);
