import { mapEnumToOptions } from '@abp/ng.core';

export enum TaxAddDeduct {
  Add = 0,
  Deduct = 1,
}

export const taxAddDeductOptions = mapEnumToOptions(TaxAddDeduct);
