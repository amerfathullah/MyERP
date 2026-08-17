import { mapEnumToOptions } from '@abp/ng.core';

export enum EmailCampaignFor {
  Lead = 0,
  Contact = 1,
}

export const emailCampaignForOptions = mapEnumToOptions(EmailCampaignFor);
