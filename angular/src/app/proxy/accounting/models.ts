import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AccountType } from './account-type.enum';
import type { AccountSubType } from './account-sub-type.enum';
import type { PaymentType } from './payment-type.enum';
import type { FinancialReportDataSource } from './entities/financial-report-data-source.enum';
import type { FinancialReportType } from './entities/financial-report-type.enum';

export interface AccountCategoryDto {
  id?: string;
  name?: string;
  rootType?: string;
  description?: string | null;
}

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

export interface AccountingDimensionDto {
  id?: string;
  documentType?: string;
  label?: string;
  fieldName?: string;
  isEnabled?: boolean;
  isMandatory?: boolean;
  companyId?: string | null;
}

export interface AccountingDimensionFilterDto {
  id?: string;
  accountingDimensionId?: string;
  accountId?: string;
  companyId?: string;
  isAllowList?: boolean;
  dimensionValueIds?: string;
}

export interface AccountingPeriodDto extends EntityDto<string> {
  companyId?: string;
  periodName?: string;
  startDate?: string;
  endDate?: string;
  isClosed?: boolean;
}

export interface AgingReportDto {
  reportType?: string;
  asOfDate?: string;
  bucketLabels?: string[];
  bucketTotals?: number[];
  totalOutstanding?: number;
  invoiceCount?: number;
}

export interface AgingReportRequestDto {
  companyId?: string;
  asOfDate?: string | null;
}

export interface AutoMatchResult {
  matchedCount?: number;
  partiallyReconciledCount?: number;
  unmatchedCount?: number;
}

export interface AutoMatchResultDto {
  matchedCount?: number;
  partiallyReconciledCount?: number;
  unmatchedCount?: number;
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

export interface BankReconciliationStatementDto {
  glBalance?: number;
  outstandingDeposits?: number;
  outstandingPayments?: number;
  netOutstanding?: number;
  calculatedBankBalance?: number;
  unclearedEntries?: BankStatementEntryDto[];
  currencyCode?: string;
  reportDate?: string;
  bankAccountName?: string;
}

export interface BankReconciliationSummaryDto {
  totalTransactions?: number;
  reconciledCount?: number;
  unreconciledCount?: number;
  totalDeposits?: number;
  totalWithdrawals?: number;
  unreconciledBalance?: number;
}

export interface BankStatementEntryDto {
  postingDate?: string;
  documentType?: string;
  documentNumber?: string;
  documentId?: string;
  debit?: number;
  credit?: number;
  referenceNumber?: string | null;
  clearanceDate?: string | null;
  partyName?: string | null;
}

export interface BankStatementImportInput {
  companyId?: string;
  bankAccountId?: string;
  csvContent?: string;
  tenantId?: string | null;
  currencyCode?: string | null;
}

export interface BankStatementImportResult {
  importedCount?: number;
  skippedCount?: number;
  errors?: string[];
  success?: boolean;
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

export interface BankTransactionRuleDto extends EntityDto<string> {
  companyId?: string;
  ruleName?: string;
  priority?: number;
  isEnabled?: boolean;
  transactionType?: number;
  minAmount?: number | null;
  maxAmount?: number | null;
  classifyAs?: number;
  descriptionContains?: string | null;
}

export interface BatchPaymentInvoiceDto {
  invoiceId?: string;
  invoiceNumber?: string;
  invoiceType?: string;
  partyId?: string;
  issueDate?: string;
  dueDate?: string | null;
  grandTotal?: number;
  outstanding?: number;
  currencyCode?: string;
}

export interface BatchPaymentItemDto {
  partyId?: string;
  invoiceId?: string;
  invoiceType?: string;
  totalAmount?: number;
  outstanding?: number;
  amount?: number;
  exchangeRate?: number;
}

export interface BatchPaymentResultDto {
  successCount?: number;
  errorCount?: number;
  totalAmount?: number;
  errors?: string[];
  createdPaymentEntryIds?: string[];
}

export interface BudgetVarianceReportDto {
  companyId?: string;
  fiscalYearId?: string;
  fromDate?: string;
  toDate?: string;
  rows?: BudgetVarianceRowDto[];
  totalBudget?: number;
  totalActual?: number;
  totalVariance?: number;
  overBudgetCount?: number;
}

export interface BudgetVarianceRequestDto {
  companyId?: string;
  fiscalYearId?: string;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface BudgetVarianceRowDto {
  accountId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: string;
  budgetAmount?: number;
  actualAmount?: number;
  variance?: number;
  variancePercent?: number;
  isOverBudget?: boolean;
}

export interface CashFlowLineItem {
  label?: string;
  amount?: number;
}

export interface CashFlowRequestDto {
  companyId?: string;
  fromDate?: string;
  toDate?: string;
}

export interface CashFlowStatementDto {
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  operatingActivities?: CashFlowLineItem[];
  operatingTotal?: number;
  investingActivities?: CashFlowLineItem[];
  investingTotal?: number;
  financingActivities?: CashFlowLineItem[];
  financingTotal?: number;
  netCashChange?: number;
  openingCashBalance?: number;
  closingCashBalance?: number;
}

export interface CostCenterAllocationDto {
  id?: string;
  companyId?: string;
  mainCostCenterId?: string;
  validFrom?: string;
  isActive?: boolean;
  entries?: CostCenterAllocationEntryDto[];
}

export interface CostCenterAllocationEntryDto {
  id?: string;
  childCostCenterId?: string;
  percentage?: number;
}

export interface CostCenterDto extends AuditedEntityDto<string> {
  name?: string;
  costCenterNumber?: string | null;
  companyId?: string;
  isGroup?: boolean;
  parentId?: string | null;
  isActive?: boolean;
}

export interface CostCenterPLRowDto {
  costCenterId?: string;
  costCenterName?: string;
  revenue?: number;
  expense?: number;
  netProfit?: number;
  profitMargin?: number;
}

export interface CreateAccountCategoryDto {
  name?: string;
  rootType?: string;
  description?: string | null;
}

export interface CreateAccountingDimensionDto {
  documentType: string;
  label: string;
  isMandatory?: boolean;
  companyId?: string | null;
}

export interface CreateBankTransactionRuleDto {
  companyId?: string;
  ruleName?: string;
  transactionType?: number;
  minAmount?: number | null;
  maxAmount?: number | null;
  classifyAs?: number;
  descriptionContains?: string | null;
}

export interface CreateBatchPaymentDto {
  companyId: string;
  paymentType?: PaymentType;
  partyType?: string;
  paidFromAccountId: string;
  paidToAccountId: string;
  modeOfPaymentId?: string | null;
  postingDate?: string | null;
  groupByParty?: boolean;
  items: BatchPaymentItemDto[];
}

export interface CreateCostCenterAllocationDto {
  companyId?: string;
  mainCostCenterId?: string;
  validFrom?: string;
  entries?: CreateCostCenterAllocationEntryDto[];
}

export interface CreateCostCenterAllocationEntryDto {
  childCostCenterId?: string;
  percentage?: number;
}

export interface CreateCostCenterDto {
  companyId: string;
  name: string;
  costCenterNumber?: string | null;
  isGroup?: boolean;
  parentId?: string | null;
}

export interface CreateCurrencyExchangeDto {
  fromCurrency?: string;
  toCurrency?: string;
  exchangeRate?: number;
  date?: string;
}

export interface CreateDimensionFilterDto {
  accountingDimensionId?: string;
  accountId?: string;
  companyId?: string;
  isAllowList?: boolean;
  dimensionValueIds?: string | null;
}

export interface CreateFinanceBookDto {
  companyId?: string;
  name?: string;
  isDefault?: boolean;
  description?: string | null;
}

export interface CreateFinancialReportRowDto {
  label?: string;
  dataSource?: FinancialReportDataSource;
  sortOrder?: number;
  referenceCode?: string | null;
  calculationFormula?: string | null;
  accountCategoryFilter?: string | null;
  customApiPath?: string | null;
  hideWhenEmpty?: boolean;
  isBold?: boolean;
  indentLevel?: number;
  signMultiplier?: number;
}

export interface CreateFinancialReportTemplateDto {
  name?: string;
  reportType?: FinancialReportType;
  companyId?: string | null;
  description?: string | null;
  rows?: CreateFinancialReportRowDto[];
}

export interface CreateFiscalYearDto {
  companyId?: string;
  name?: string;
  startDate?: string;
  endDate?: string;
}

export interface CreateInternalTransferDto {
  bankTransactionId: string;
  targetBankAccountGlId: string;
  companyId: string;
  mirrorTransactionId?: string | null;
}

export interface CreateInvoiceDiscountingDto {
  companyId?: string;
  annualDiscountRate?: number;
  daysToMaturity?: number;
  bankAccountId?: string;
  discountExpenseAccountId?: string;
  shortTermLoanAccountId?: string;
  receivableAccountId?: string;
  invoices?: InvoiceForDiscountingDto[];
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

export interface CreateOpeningInvoicesDto {
  companyId: string;
  postingDate: string;
  currency?: string | null;
  invoices: OpeningInvoiceLineDto[];
}

export interface CreateOpeningJournalEntryDto {
  companyId: string;
  postingDate: string;
  lines: OpeningJournalLineDto[];
  remarks?: string | null;
}

export interface CreatePEFromTransactionDto {
  bankTransactionId: string;
  companyId: string;
  partyType: string;
  partyId: string;
  bankAccountId: string;
  partyAccountId: string;
  againstInvoiceId?: string | null;
  modeOfPaymentId?: string | null;
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
  references?: PaymentReferenceDto[] | null;
  againstOrderId?: string | null;
  againstOrderType?: string | null;
  exchangeRate?: number;
  paymentCurrency?: string | null;
}

export interface CreatePaymentRequestDto {
  companyId?: string;
  paymentRequestType?: string;
  referenceDoctype?: string;
  referenceId?: string;
  partyId?: string;
  partyType?: string;
  partyName?: string | null;
  grandTotal?: number;
  currency?: string;
  emailTo?: string | null;
  subject?: string | null;
  message?: string | null;
}

export interface CreatePaymentTermDto {
  invoicePortion?: number;
  creditDays?: number;
  description?: string | null;
  modeOfPaymentId?: string | null;
}

export interface CreatePeriodClosingVoucherDto {
  companyId?: string;
  fiscalYearId?: string;
  postingDate?: string;
  transactionDate?: string;
  closingAccountId?: string;
  remarks?: string | null;
}

export interface CreateRevaluationDto {
  companyId?: string;
  postingDate?: string;
  roundingLossAllowance?: number;
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

export interface CreateUpdatePaymentTermsTemplateDto {
  name?: string;
  terms?: CreatePaymentTermDto[];
}

export interface CurrencyExchangeDto extends EntityDto<string> {
  fromCurrency?: string;
  toCurrency?: string;
  exchangeRate?: number;
  date?: string;
}

export interface EligibleAccountDto {
  accountId?: string;
  accountName?: string;
  accountCurrency?: string;
  balanceInAccountCurrency?: number;
  currentExchangeRate?: number;
  balanceInCompanyCurrency?: number;
  gainLoss?: number;
}

export interface EvaluateRulesDto {
  companyId?: string;
  forceReEvaluate?: boolean;
}

export interface ExchangeRateRevaluationDto extends EntityDto<string> {
  companyId?: string;
  postingDate?: string;
  totalGainLoss?: number;
  entryCount?: number;
}

export interface ExecuteReportDto {
  templateId?: string;
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  financeBook?: string | null;
}

export interface FinanceBookDto {
  id?: string;
  companyId?: string;
  name?: string;
  isDefault?: boolean;
  description?: string | null;
}

export interface FinancialReportResultDto {
  templateName?: string;
  reportType?: string;
  fromDate?: string;
  toDate?: string;
  grandTotal?: number;
  rows?: FinancialReportResultRowDto[];
}

export interface FinancialReportResultRowDto {
  label?: string;
  value?: number;
  indentLevel?: number;
  isBold?: boolean;
  referenceCode?: string | null;
  dataSource?: string;
}

export interface FinancialReportRowDto {
  id?: string;
  label?: string;
  dataSource?: FinancialReportDataSource;
  sortOrder?: number;
  referenceCode?: string | null;
  calculationFormula?: string | null;
  accountCategoryFilter?: string | null;
  customApiPath?: string | null;
  hideWhenEmpty?: boolean;
  isBold?: boolean;
  indentLevel?: number;
  signMultiplier?: number;
}

export interface FinancialReportTemplateDto {
  id?: string;
  name?: string;
  reportType?: FinancialReportType;
  companyId?: string | null;
  isStandard?: boolean;
  isEnabled?: boolean;
  description?: string | null;
  rows?: FinancialReportRowDto[];
}

export interface FiscalYearDto extends EntityDto<string> {
  companyId?: string;
  name?: string;
  startDate?: string;
  endDate?: string;
  isClosed?: boolean;
}

export interface FreezeAccountingPeriodDto {
  companyId?: string;
  freezeUpTo?: string;
}

export interface GeneralLedgerFilterDto {
  companyId?: string;
  accountId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  partyType?: string | null;
  partyId?: string | null;
  voucherNumber?: string | null;
  costCenterId?: string | null;
}

export interface GeneralLedgerLineDto {
  id?: string;
  postingDate?: string;
  accountCode?: string | null;
  accountName?: string | null;
  voucherType?: string | null;
  voucherId?: string | null;
  voucherNumber?: string | null;
  debitAmount?: number;
  creditAmount?: number;
  balance?: number;
  partyType?: string | null;
  partyName?: string | null;
  costCenterName?: string | null;
  description?: string | null;
}

export interface GeneralLedgerReportDto {
  entries?: GeneralLedgerLineDto[];
  totalDebit?: number;
  totalCredit?: number;
  balance?: number;
  count?: number;
}

export interface GetBankReconciliationStatementInput {
  bankAccountId: string;
  companyId: string;
  reportDate: string;
}

export interface GetBankTransactionsDto extends PagedAndSortedResultRequestDto {
  bankAccountId: string;
  isReconciled?: boolean | null;
  dateFrom?: string | null;
  dateTo?: string | null;
}

export interface GetCostCenterListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
}

export interface GetOutstandingForBatchDto {
  companyId?: string;
  partyType?: string;
  partyId?: string;
}

export interface ImportBankTransactionDto {
  companyId: string;
  bankAccountId: string;
  transactionDate: string;
  description: string;
  amount: number;
  referenceNumber?: string | null;
}

export interface InternalTransferResultDto {
  paymentEntryId?: string;
  paymentNumber?: string | null;
  sourceTransactionId?: string;
  mirrorTransactionId?: string | null;
}

export interface InvoiceDiscountingDto {
  id?: string;
  companyId?: string;
  totalOutstanding?: number;
  discountCharge?: number;
  disbursementAmount?: number;
  status?: number;
  disbursementJournalEntryId?: string | null;
  settlementJournalEntryId?: string | null;
}

export interface InvoiceForDiscountingDto {
  invoiceId?: string;
  invoiceNumber?: string;
  outstandingAmount?: number;
  isAlreadyDiscounted?: boolean;
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
  accountName?: string | null;
  accountCode?: string | null;
  amount?: number;
  isDebit?: boolean;
  description?: string | null;
}

export interface MatchCandidate {
  paymentEntryId?: string;
  paymentNumber?: string | null;
  amount?: number;
  postingDate?: string;
  referenceNumber?: string | null;
  rank?: number;
}

export interface MatchCandidateDto {
  paymentEntryId?: string;
  paymentNumber?: string | null;
  amount?: number;
  postingDate?: string;
  referenceNumber?: string | null;
  rank?: number;
}

export interface MirrorTransactionDto {
  transactionId?: string;
  bankAccountId?: string;
  referenceNumber?: string | null;
  transactionDate?: string;
  deposit?: number;
  withdrawal?: number;
  currencyCode?: string;
}

export interface ModeOfPaymentDto extends EntityDto<string> {
  name?: string;
  type?: string;
}

export interface MonthEndCheckDto {
  name?: string;
  passed?: boolean;
  details?: string | null;
}

export interface MonthEndCloseRequestDto {
  companyId?: string;
  periodEndDate?: string;
}

export interface MonthEndCloseStatusDto {
  companyId?: string;
  periodEndDate?: string;
  isTrialBalanceBalanced?: boolean;
  hasPeriodClosingVoucher?: boolean;
  isPeriodClosed?: boolean;
  isFullyClosed?: boolean;
}

export interface MonthEndReadinessDto {
  companyId?: string;
  periodEndDate?: string;
  isReady?: boolean;
  passedCount?: number;
  totalChecks?: number;
  checks?: MonthEndCheckDto[];
}

export interface OpeningBalanceResultDto {
  journalEntryId?: string;
  entryNumber?: string;
  totalDebit?: number;
  totalCredit?: number;
  temporaryOpeningAmount?: number;
  message?: string;
}

export interface OpeningInvoiceLineDto {
  customerId?: string | null;
  supplierId?: string | null;
  itemId?: string | null;
  outstandingAmount: number;
  dueDate?: string | null;
}

export interface OpeningInvoiceResultDto {
  created?: number;
  failed?: number;
  errors?: string[];
  message?: string;
}

export interface OpeningJournalLineDto {
  accountId: string;
  debit?: number;
  credit?: number;
  partyType?: string | null;
  partyId?: string | null;
}

export interface OpeningStatusDto {
  companyId?: string;
  temporaryOpeningBalance?: number;
  isBalanced?: boolean;
  openingSalesInvoiceCount?: number;
  openingPurchaseInvoiceCount?: number;
  openingJournalEntryCount?: number;
  message?: string;
}

export interface OutstandingInvoiceDto {
  voucherId?: string;
  voucherType?: string;
  outstanding?: number;
}

export interface OutstandingInvoiceForPaymentDto {
  invoiceId?: string;
  invoiceNumber?: string;
  issueDate?: string;
  dueDate?: string | null;
  grandTotal?: number;
  outstanding?: number;
  currencyCode?: string;
  invoiceType?: string;
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
  partyType?: string | null;
  partyId?: string | null;
  partyName?: string | null;
}

export interface PaymentReferenceDto {
  referenceType: string;
  referenceId: string;
  allocatedAmount: number;
  exchangeRate?: number;
}

export interface PaymentRequestDto extends EntityDto<string> {
  companyId?: string;
  paymentRequestType?: string;
  referenceDoctype?: string;
  referenceId?: string;
  partyId?: string;
  partyType?: string;
  partyName?: string | null;
  grandTotal?: number;
  outstandingAmount?: number;
  currency?: string;
  status?: number;
  paymentEntryId?: string | null;
}

export interface PaymentTermDto {
  id?: string;
  invoicePortion?: number;
  creditDays?: number;
  description?: string | null;
  modeOfPaymentId?: string | null;
}

export interface PaymentTermsTemplateDto extends EntityDto<string> {
  name?: string;
  isActive?: boolean;
  terms?: PaymentTermDto[];
}

export interface PeriodClosingVoucherDto extends EntityDto<string> {
  companyId?: string;
  fiscalYearId?: string;
  voucherNumber?: string | null;
  postingDate?: string;
  transactionDate?: string;
  closingAccountId?: string;
  totalClosingAmount?: number;
  status?: number;
  remarks?: string | null;
  entryCount?: number;
}

export interface ProfitLossByCostCenterDto {
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  totalRevenue?: number;
  totalExpense?: number;
  netProfit?: number;
  overallMargin?: number;
  costCenters?: CostCenterPLRowDto[];
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

export interface ReconcileAllocationDto {
  paymentVoucherId: string;
  paymentVoucherType: string;
  invoiceVoucherId: string;
  invoiceVoucherType: string;
  allocatedAmount: number;
}

export interface ReconcileBankTransactionDto {
  transactionId: string;
  paymentEntryId: string;
  matchedDocumentRef?: string | null;
}

export interface ReconcilePaymentDto {
  partyType: string;
  partyId: string;
  companyId: string;
  allocations: ReconcileAllocationDto[];
}

export interface StatementEntryDto {
  date?: string;
  documentType?: string;
  documentNumber?: string;
  documentId?: string;
  debitAmount?: number;
  creditAmount?: number;
  runningBalance?: number;
}

export interface StatementOfAccountsDto {
  customerId?: string;
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  openingBalance?: number;
  closingBalance?: number;
  totalDebit?: number;
  totalCredit?: number;
  entries?: StatementEntryDto[];
}

export interface SupplierStatementDto {
  supplierId?: string;
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  openingBalance?: number;
  closingBalance?: number;
  totalInvoiced?: number;
  totalPaid?: number;
  entries?: StatementEntryDto[];
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

export interface UnreconcileDto {
  paymentVoucherType: string;
  paymentVoucherId: string;
  invoiceVoucherType: string;
  invoiceVoucherId: string;
}

export interface UomConversionDto extends EntityDto<string> {
  fromUom?: string;
  toUom?: string;
  conversionFactor?: number;
  itemId?: string | null;
}

export interface UpdateAccountingDimensionDto {
  label: string;
  isMandatory?: boolean;
  hideDisabledValues?: boolean;
  companyId?: string | null;
}

export interface VoucherCreatedResultDto {
  paymentEntryId?: string;
  paymentNumber?: string;
  amount?: number;
  paymentType?: string;
  bankTransactionId?: string;
  isReconciled?: boolean;
}

export interface VoucherLedgerDto {
  voucherType?: string;
  voucherId?: string;
  voucherNumber?: string | null;
  entries?: VoucherLedgerEntryDto[];
  totalDebit?: number;
  totalCredit?: number;
  isBalanced?: boolean;
}

export interface VoucherLedgerEntryDto {
  postingDate?: string;
  accountCode?: string | null;
  accountName?: string | null;
  debitAmount?: number;
  creditAmount?: number;
  costCenterName?: string | null;
  description?: string | null;
  financeBook?: string | null;
}
