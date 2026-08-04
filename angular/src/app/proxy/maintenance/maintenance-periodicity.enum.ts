import { mapEnumToOptions } from '@abp/ng.core';

export enum MaintenancePeriodicity {
  Weekly = 0,
  Monthly = 1,
  Quarterly = 2,
  HalfYearly = 3,
  Yearly = 4,
  TwoYearly = 5,
  ThreeYearly = 6,
  Random = 7,
}

export const maintenancePeriodicityOptions = mapEnumToOptions(MaintenancePeriodicity);
