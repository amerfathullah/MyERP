import { mapEnumToOptions } from '@abp/ng.core';

export enum MaintenancePeriodicity {
  Daily = 0,
  Weekly = 1,
  Monthly = 2,
  Quarterly = 3,
  HalfYearly = 4,
  Yearly = 5,
  TwoYearly = 6,
  ThreeYearly = 7,
  Random = 8,
}

export const maintenancePeriodicityOptions = mapEnumToOptions(MaintenancePeriodicity);
