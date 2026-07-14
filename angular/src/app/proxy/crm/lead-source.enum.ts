import { mapEnumToOptions } from '@abp/ng.core';

export enum LeadSource {
  Website = 0,
  Referral = 1,
  Campaign = 2,
  ColdCall = 3,
  Advertisement = 4,
  SocialMedia = 5,
  TradeShow = 6,
  Partner = 7,
  Other = 8,
}

export const leadSourceOptions = mapEnumToOptions(LeadSource);
