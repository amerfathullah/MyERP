import { mapEnumToOptions } from '@abp/ng.core';

export enum BarcodeType {
  Ean = 0,
  Upca = 1,
  Code128 = 2,
  Other = 3,
}

export const barcodeTypeOptions = mapEnumToOptions(BarcodeType);
