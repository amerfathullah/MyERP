import { mapEnumToOptions } from '@abp/ng.core';

export enum MaintenanceVisitStatus {
  Open = 0,
  PartiallyCompleted = 1,
  Completed = 2,
  Cancelled = 3,
}

export const maintenanceVisitStatusOptions = mapEnumToOptions(MaintenanceVisitStatus);
