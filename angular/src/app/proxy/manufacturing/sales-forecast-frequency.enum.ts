import { mapEnumToOptions } from '@abp/ng.core';

export enum SalesForecastFrequency {
  Weekly = 0,
  Monthly = 1,
}

export const salesForecastFrequencyOptions = mapEnumToOptions(SalesForecastFrequency);
