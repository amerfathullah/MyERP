import { mapEnumToOptions } from '@abp/ng.core';

export enum JournalEntryVoucherType {
  JournalEntry = 0,
  InterCompanyJournalEntry = 1,
  BankEntry = 2,
  CashEntry = 3,
  CreditCardEntry = 4,
  DebitNote = 5,
  CreditNote = 6,
  ContraEntry = 7,
  ExciseEntry = 8,
  WriteOffEntry = 9,
  OpeningEntry = 10,
  DepreciationEntry = 11,
  ExchangeRateRevaluation = 12,
  ExchangeGainOrLoss = 13,
  DeferredRevenue = 14,
  DeferredExpense = 15,
  Reversal = 16,
  PeriodClosing = 17,
  PaymentTax = 18,
}

export const journalEntryVoucherTypeOptions = mapEnumToOptions(JournalEntryVoucherType);
