import { mapEnumToOptions } from '@abp/ng.core';

export enum ChequeSize {
  Regular = 0,
  A4 = 1,
}

export const chequeSizeOptions = mapEnumToOptions(ChequeSize);
