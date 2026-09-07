import { mapEnumToOptions } from '@abp/ng.core';

export enum EInvoiceDocumentType {
  Invoice = 1,
  CreditNote = 2,
  DebitNote = 3,
  RefundNote = 4,
  SelfBilledInvoice = 11,
  SelfBilledCreditNote = 12,
  SelfBilledDebitNote = 13,
  SelfBilledRefundNote = 14,
}

export const eInvoiceDocumentTypeOptions = mapEnumToOptions(EInvoiceDocumentType);
