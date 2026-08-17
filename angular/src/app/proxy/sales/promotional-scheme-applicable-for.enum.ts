import { mapEnumToOptions } from '@abp/ng.core';

export enum PromotionalSchemeApplicableFor {
  None = 0,
  Customer = 1,
  CustomerGroup = 2,
  Territory = 3,
  SalesPartner = 4,
  Campaign = 5,
  Supplier = 6,
  SupplierGroup = 7,
}

export const promotionalSchemeApplicableForOptions = mapEnumToOptions(PromotionalSchemeApplicableFor);
