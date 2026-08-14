import { mapEnumToOptions } from '@abp/ng.core';

export enum DeliveryTripStatus {
  Draft = 0,
  Scheduled = 1,
  InTransit = 2,
  Completed = 3,
  Cancelled = 4,
}

export const deliveryTripStatusOptions = mapEnumToOptions(DeliveryTripStatus);
