import { mapEnumToOptions } from '@abp/ng.core';

export enum LeadStatus {
  New = 0,
  Open = 1,
  Replied = 2,
  Interested = 3,
  Qualified = 4,
  Converted = 5,
  Lost = 6,
  DoNotContact = 7,
}

export const leadStatusOptions = mapEnumToOptions(LeadStatus);
