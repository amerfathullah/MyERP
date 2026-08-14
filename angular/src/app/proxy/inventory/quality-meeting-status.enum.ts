import { mapEnumToOptions } from '@abp/ng.core';

export enum QualityMeetingStatus {
  Open = 0,
  Closed = 1,
}

export const qualityMeetingStatusOptions = mapEnumToOptions(QualityMeetingStatus);
