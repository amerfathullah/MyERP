import { mapEnumToOptions } from '@abp/ng.core';

export enum SubAssemblyType {
  InHouseManufacturing = 0,
  Subcontracting = 1,
  MaterialRequest = 2,
}

export const subAssemblyTypeOptions = mapEnumToOptions(SubAssemblyType);
