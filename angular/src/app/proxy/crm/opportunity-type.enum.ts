import { mapEnumToOptions } from '@abp/ng.core';

export enum OpportunityType {
  Sales = 0,
  Support = 1,
  Maintenance = 2,
}

export const opportunityTypeOptions = mapEnumToOptions(OpportunityType);
