import { mapEnumToOptions } from '@abp/ng.core';

export enum ProformaInvoiceStatus {
  Draft = 0,
  Issued = 1,
  Cancelled = 2,
}

export const proformaInvoiceStatusOptions = mapEnumToOptions(ProformaInvoiceStatus);
