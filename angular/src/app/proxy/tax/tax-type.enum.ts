import { mapEnumToOptions } from '@abp/ng.core';

export enum TaxType {
  Sales = 0,
  Service = 1,
  Exempt = 2,
  ZeroRated = 3,
  OutOfScope = 4,
}

export const taxTypeOptions = mapEnumToOptions(TaxType);
