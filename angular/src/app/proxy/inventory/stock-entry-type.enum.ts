import { mapEnumToOptions } from '@abp/ng.core';

export enum StockEntryType {
  Receipt = 0,
  Issue = 1,
  Transfer = 2,
  Adjustment = 3,
}

export const stockEntryTypeOptions = mapEnumToOptions(StockEntryType);
