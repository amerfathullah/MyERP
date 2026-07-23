import { mapEnumToOptions } from '@abp/ng.core';

export enum ProformaInvoiceBasis {
  Quantity = 0,
  Amount = 1,
}

export const proformaInvoiceBasisOptions = mapEnumToOptions(ProformaInvoiceBasis);
