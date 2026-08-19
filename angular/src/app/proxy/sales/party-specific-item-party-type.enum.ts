import { mapEnumToOptions } from '@abp/ng.core';

export enum PartySpecificItemPartyType {
  Customer = 0,
  CustomerGroup = 1,
  Supplier = 2,
  SupplierGroup = 3,
}

export const partySpecificItemPartyTypeOptions = mapEnumToOptions(PartySpecificItemPartyType);
