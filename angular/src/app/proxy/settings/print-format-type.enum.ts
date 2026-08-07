import { mapEnumToOptions } from '@abp/ng.core';

export enum PrintFormatType {
  Custom = 0,
  Builder = 1,
  BuilderV2 = 2,
}

export const printFormatTypeOptions = mapEnumToOptions(PrintFormatType);
