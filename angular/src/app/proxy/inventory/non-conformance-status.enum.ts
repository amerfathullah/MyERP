import { mapEnumToOptions } from '@abp/ng.core';

export enum NonConformanceStatus {
  Open = 0,
  Resolved = 1,
  Cancelled = 2,
}

export const nonConformanceStatusOptions = mapEnumToOptions(NonConformanceStatus);
