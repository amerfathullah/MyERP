import { mapEnumToOptions } from '@abp/ng.core';

export enum PaymentType {
  Receive = 0,
  Pay = 1,
  InternalTransfer = 2,
}

export const paymentTypeOptions = mapEnumToOptions(PaymentType);
