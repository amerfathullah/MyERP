import { mapEnumToOptions } from '@abp/ng.core';

export enum AnalyticsGroupBy {
  Customer = 0,
  Item = 1,
  Territory = 2,
  SalesPerson = 3,
  ItemGroup = 4,
}

export const analyticsGroupByOptions = mapEnumToOptions(AnalyticsGroupBy);
