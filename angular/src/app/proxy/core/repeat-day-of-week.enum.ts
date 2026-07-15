import { mapEnumToOptions } from '@abp/ng.core';

export enum RepeatDayOfWeek {
  Monday = 1,
  Tuesday = 2,
  Wednesday = 3,
  Thursday = 4,
  Friday = 5,
  Saturday = 6,
  Sunday = 7,
}

export const repeatDayOfWeekOptions = mapEnumToOptions(RepeatDayOfWeek);
