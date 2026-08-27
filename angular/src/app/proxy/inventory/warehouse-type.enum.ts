import { mapEnumToOptions } from '@abp/ng.core';

export enum WarehouseType {
  Standard = 0,
  Transit = 1,
  Rejected = 2,
  SampleRetention = 3,
}

export const warehouseTypeOptions = mapEnumToOptions(WarehouseType);
