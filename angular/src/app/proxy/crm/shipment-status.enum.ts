import { mapEnumToOptions } from '@abp/ng.core';

export enum ShipmentStatus {
  Draft = 0,
  Booked = 1,
  InTransit = 2,
  Delivered = 3,
  Cancelled = 4,
}

export const shipmentStatusOptions = mapEnumToOptions(ShipmentStatus);
