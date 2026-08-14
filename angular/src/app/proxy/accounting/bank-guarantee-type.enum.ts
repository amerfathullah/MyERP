import { mapEnumToOptions } from '@abp/ng.core';

export enum BankGuaranteeType {
  Receiving = 1,
  Providing = 2,
}

export const bankGuaranteeTypeOptions = mapEnumToOptions(BankGuaranteeType);
