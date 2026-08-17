import { mapEnumToOptions } from '@abp/ng.core';

export enum PaymentOrderType {
  PaymentRequest = 0,
  PaymentEntry = 1,
}

export const paymentOrderTypeOptions = mapEnumToOptions(PaymentOrderType);
