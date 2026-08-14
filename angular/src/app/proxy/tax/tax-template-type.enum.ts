import { mapEnumToOptions } from '@abp/ng.core';

export enum TaxTemplateType {
  Selling = 0,
  Buying = 1,
}

export const taxTemplateTypeOptions = mapEnumToOptions(TaxTemplateType);
