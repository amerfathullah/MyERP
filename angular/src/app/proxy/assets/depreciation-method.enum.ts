import { mapEnumToOptions } from '@abp/ng.core';

export enum DepreciationMethod {
  StraightLine = 0,
  DoubleDecliningBalance = 1,
  WrittenDownValue = 2,
  Manual = 3,
}

export const depreciationMethodOptions = mapEnumToOptions(DepreciationMethod);
