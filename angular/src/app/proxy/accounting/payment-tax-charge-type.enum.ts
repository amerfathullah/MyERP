import { mapEnumToOptions } from '@abp/ng.core';

export enum PaymentTaxChargeType {
  OnPaidAmount = 0,
  Actual = 1,
}

export const paymentTaxChargeTypeOptions = mapEnumToOptions(PaymentTaxChargeType);
