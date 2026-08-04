import { mapEnumToOptions } from '@abp/ng.core';

export enum TrackingBoardStage {
  Ordered = 0,
  PartiallyDelivered = 1,
  FullyDelivered = 2,
  Completed = 3,
}

export const trackingBoardStageOptions = mapEnumToOptions(TrackingBoardStage);
