import { mapEnumToOptions } from '@abp/ng.core';

export enum EmailCampaignStatus {
  Scheduled = 0,
  InProgress = 1,
  Completed = 2,
  Unsubscribed = 3,
}

export const emailCampaignStatusOptions = mapEnumToOptions(EmailCampaignStatus);
