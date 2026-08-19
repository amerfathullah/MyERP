import { mapEnumToOptions } from '@abp/ng.core';

export enum SupplierHoldType {
  None = 0,
  All = 1,
  Invoices = 2,
  Payments = 3,
}

export const supplierHoldTypeOptions = mapEnumToOptions(SupplierHoldType);
