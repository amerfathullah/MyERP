import { mapEnumToOptions } from '@abp/ng.core';

export enum TaxDeductionBasis {
  NetTotal = 0,
  GrossTotal = 1,
}

export const taxDeductionBasisOptions = mapEnumToOptions(TaxDeductionBasis);
