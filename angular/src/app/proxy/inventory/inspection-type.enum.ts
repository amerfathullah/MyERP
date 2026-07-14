import { mapEnumToOptions } from '@abp/ng.core';

export enum InspectionType {
  Incoming = 0,
  Outgoing = 1,
  InProcess = 2,
}

export const inspectionTypeOptions = mapEnumToOptions(InspectionType);
