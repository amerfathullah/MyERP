import { mapEnumToOptions } from '@abp/ng.core';

export enum OpportunityStatus {
  Open = 0,
  Replied = 1,
  Quotation = 2,
  Converted = 3,
  Lost = 4,
  Closed = 5,
}

export const opportunityStatusOptions = mapEnumToOptions(OpportunityStatus);
