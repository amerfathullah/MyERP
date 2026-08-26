import { mapEnumToOptions } from '@abp/ng.core';

export enum CommunicationMediumType {
  Voice = 0,
  Email = 1,
  Chat = 2,
}

export const communicationMediumTypeOptions = mapEnumToOptions(CommunicationMediumType);
