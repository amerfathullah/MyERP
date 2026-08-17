import { mapEnumToOptions } from '@abp/ng.core';

export enum ShareTransferType {
  Issue = 0,
  Purchase = 1,
  Transfer = 2,
}

export const shareTransferTypeOptions = mapEnumToOptions(ShareTransferType);
