import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AccountType } from './account-type.enum';
import type { AccountSubType } from './account-sub-type.enum';
import type { PaymentType } from './payment-type.enum';

export interface AccountDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: AccountType;
  accountSubType?: AccountSubType | null;
  parentAccountId?: string | null;
  isGroup?: boolean;
  currency?: string | null;
  description?: string | null;
  isFrozen?: boolean;
  isActive?: boolean;
}

export interface BalanceSheetReportDto {
  asOfDate?: string;
  companyId?: string;
  assetRows?: BalanceSheetRowDto[];
  liabilityRows?: BalanceSheetRowDto[];
  equityRows?: BalanceSheetRowDto[];
  totalAssets?: number;
  totalLiabilities?: number;
  totalEquity?: number;
}

export interface BalanceSheetRequestDto {
  companyId: string;
  asOfDate: string;
}

export interface BalanceSheetRowDto {
  accountId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: string;
  amount?: number;
  level?: number;
  isGroup?: boolean;
}

export interface BankReconciliationSummaryDto {
  totalTransactions?: number;
  reconciledCount?: number;
  unreconciledCount?: number;
  totalDeposits?: number;
  totalWithdrawals?: number;
  unreconciledBalance?: number;
}

export interface BankTransactionDto extends EntityDto<string> {
  companyId?: string;
  bankAccountId?: string;
  transactionDate?: string;
  description?: string;
  amount?: number;
  referenceNumber?: string | null;
  isReconciled?: boolean;
  paymentEntryId?: string | null;
  matchedDocumentRef?: string | null;
  reconciledAt?: string | null;
}

export interface CreateJournalEntryDto {
  companyId: string;
  fiscalYearId: string;
  postingDate: string;
  referenceType?: string | null;
  referenceId?: string | null;
  referenceNumber?: string | null;
  narration?: string | null;
  lines: CreateJournalEntryLineDto[];
}

export interface CreateJournalEntryLineDto {
  accountId: string;
  amount: number;
  isDebit: boolean;
  description?: string | null;
}

export interface CreatePaymentEntryDto {
  companyId: string;
  paymentType: PaymentType;
  postingDate: string;
  paidAmount: number;
  paidFromAccountId: string;
  paidToAccountId: string;
  modeOfPayment?: string | null;
  partyType?: string | null;
  partyId?: string | null;
  referenceNumber?: string | null;
  notes?: string | null;
  againstInvoiceId?: string | null;
  againstInvoiceType?: string | null;
}

export interface CreateUpdateAccountDto {
  companyId: string;
  accountCode: string;
  accountName: string;
  accountType: AccountType;
  accountSubType?: AccountSubType | null;
  parentAccountId?: string | null;
  isGroup?: boolean;
  currency?: string | null;
  description?: string | null;
  isFrozen?: boolean;
  isActive?: boolean;
}

export interface GetBankTransactionsDto extends PagedAndSortedResultRequestDto {
  bankAccountId: string;
  isReconciled?: boolean | null;
  dateFrom?: string | null;
  dateTo?: string | null;
}

export interface ImportBankTransactionDto {
  companyId: string;
  bankAccountId: string;
  transactionDate: string;
  description: string;
  amount: number;
  referenceNumber?: string | null;
}

export interface JournalEntryDto extends EntityDto<string> {
  companyId?: string;
  fiscalYearId?: string;
  entryNumber?: string | null;
  postingDate?: string;
  referenceType?: string | null;
  referenceId?: string | null;
  referenceNumber?: string | null;
  narration?: string | null;
  status?: string;
  totalDebit?: number;
  totalCredit?: number;
  lines?: JournalEntryLineDto[];
}

export interface JournalEntryLineDto {
  id?: string;
  accountId?: string;
  amount?: number;
  isDebit?: boolean;
  description?: string | null;
}

export interface PaymentEntryDto extends EntityDto<string> {
  companyId?: string;
  paymentNumber?: string | null;
  paymentType?: string;
  postingDate?: string;
  modeOfPayment?: string | null;
  paidAmount?: number;
  currencyCode?: string;
  status?: string;
  referenceNumber?: string | null;
}

export interface ProfitLossReportDto {
  fromDate?: string;
  toDate?: string;
  companyId?: string;
  revenueRows?: ProfitLossRowDto[];
  expenseRows?: ProfitLossRowDto[];
  totalRevenue?: number;
  totalExpense?: number;
  netProfitOrLoss?: number;
}

export interface ProfitLossRequestDto {
  companyId: string;
  fromDate: string;
  toDate: string;
}

export interface ProfitLossRowDto {
  accountId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: string;
  amount?: number;
  level?: number;
  isGroup?: boolean;
}

export interface ReconcileBankTransactionDto {
  transactionId: string;
  paymentEntryId: string;
  matchedDocumentRef?: string | null;
}

export interface TrialBalanceReportDto {
  asOfDate?: string;
  companyId?: string;
  rows?: TrialBalanceRowDto[];
  totalDebit?: number;
  totalCredit?: number;
}

export interface TrialBalanceRequestDto {
  companyId: string;
  asOfDate: string;
  fiscalYearId?: string | null;
}

export interface TrialBalanceRowDto {
  accountId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: string;
  isGroup?: boolean;
  level?: number;
  openingDebit?: number;
  openingCredit?: number;
  debit?: number;
  credit?: number;
  closingDebit?: number;
  closingCredit?: number;
}

// Budget
export interface BudgetDto {
  id?: string;
  companyId?: string;
  fiscalYearId?: string;
  budgetAgainst?: string;
  budgetAgainstId?: string;
  budgetAgainstName?: string;
  status?: number;
  actionIfAnnualBudgetExceeded?: number;
  actionIfAccumulatedMonthlyBudgetExceeded?: number;
  accounts?: BudgetAccountDto[];
  creationTime?: string;
}

export interface BudgetAccountDto {
  id?: string;
  accountId?: string;
  accountName?: string;
  budgetAmount?: number;
}

export interface CreateBudgetDto {
  companyId: string;
  fiscalYearId: string;
  budgetAgainst: string;
  budgetAgainstId: string;
  budgetAgainstName?: string;
  actionIfAnnualBudgetExceeded?: number;
  actionIfAccumulatedMonthlyBudgetExceeded?: number;
  accounts: CreateBudgetAccountDto[];
}

export interface CreateBudgetAccountDto {
  accountId: string;
  accountName?: string;
  budgetAmount: number;
}

export interface GetBudgetListDto {
  companyId?: string;
  fiscalYearId?: string;
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}
