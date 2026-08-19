import { mapEnumToOptions } from '@abp/ng.core';

export enum PartySpecificItemRestrictBasedOn {
  Item = 0,
  ItemGroup = 1,
  Brand = 2,
}

export const partySpecificItemRestrictBasedOnOptions = mapEnumToOptions(PartySpecificItemRestrictBasedOn);
