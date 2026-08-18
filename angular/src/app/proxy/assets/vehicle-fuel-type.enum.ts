import { mapEnumToOptions } from '@abp/ng.core';

export enum VehicleFuelType {
  Petrol = 0,
  Diesel = 1,
  Electric = 2,
  Hybrid = 3,
  Cng = 4,
}

export const vehicleFuelTypeOptions = mapEnumToOptions(VehicleFuelType);
