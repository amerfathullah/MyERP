import { mapEnumToOptions } from '@abp/ng.core';

export enum PartyAccountType {
  Payable = 0,
  Receivable = 1,
}

export const partyAccountTypeOptions = mapEnumToOptions(PartyAccountType);
