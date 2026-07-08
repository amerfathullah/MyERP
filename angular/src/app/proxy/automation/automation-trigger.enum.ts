import { mapEnumToOptions } from '@abp/ng.core';

export enum AutomationTrigger {
  DocumentSubmitted = 0,
  DocumentApproved = 1,
  DocumentPosted = 2,
  DocumentCancelled = 3,
  PaymentReceived = 4,
  StockBelowReorder = 5,
  InvoiceOverdue = 6,
  EInvoiceValidated = 7,
  EInvoiceRejected = 8,
  ApprovalRequired = 9,
  ScheduledDaily = 100,
  ScheduledWeekly = 101,
  ScheduledMonthly = 102,
}

export const automationTriggerOptions = mapEnumToOptions(AutomationTrigger);
