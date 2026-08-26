import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AccountType } from './account-type.enum';
import type { AccountSubType } from './account-sub-type.enum';
import type { BankGuaranteeType } from './bank-guarantee-type.enum';
import type { DocumentStatus } from '../core/document-status.enum';
import type { PaymentType } from './payment-type.enum';
import type { FinancialReportDataSource } from './financial-report-data-source.enum';
import type { FinancialReportType } from './financial-report-type.enum';
import type { JournalEntryVoucherType } from './journal-entry-voucher-type.enum';
import type { PaymentOrderType } from './payment-order-type.enum';
import type { UnreconcileVoucherType } from './unreconcile-voucher-type.enum';
import type { ShareTransferType } from './share-transfer-type.enum';
import type { ChequeSize } from './cheque-size.enum';

export interface AccountCategoryDto {
  id?: string;
  name?: string;
  rootType?: string;
  description?: string | null;
}

export interface AccountClosingBalanceDto extends EntityDto<string> {
  accountId?: string;
  accountName?: string;
  accountCode?: string | null;
  closingDate?: string;
  period?: string;
  debit?: number;
  credit?: number;
  balance?: number;
  costCenterId?: string | null;
  costCenterName?: string | null;
  financeBook?: string | null;
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

export interface AccountingDimensionDto extends EntityDto<string> {
  documentType?: string;
  label?: string;
  fieldName?: string;
  isEnabled?: boolean;
  isMandatory?: boolean;
  companyId?: string | null;
}

export interface AccountingDimensionFilterDto extends EntityDto<string> {
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

export interface AgingDetailEntryDto {
  partyId?: string;
  partyName?: string | null;
  documentId?: string;
  documentNumber?: string;
  postingDate?: string;
  dueDate?: string;
  outstandingAmount?: number;
  ageDays?: number;
  bucketLabel?: string;
}

export interface AgingReportDto {
  reportType?: string;
  asOfDate?: string;
  bucketLabels?: string[];
  bucketTotals?: number[];
  totalOutstanding?: number;
  invoiceCount?: number;
  details?: AgingDetailEntryDto[];
}

export interface AgingReportRequestDto {
  companyId?: string;
  asOfDate?: string | null;
}

export interface AllocationSuggestionDto {
  invoiceId?: string;
  invoiceNumber?: string;
  invoiceType?: string;
  outstanding?: number;
  allocatedAmount?: number;
  dueDate?: string | null;
  isOverdue?: boolean;
}

export interface AutoAllocateRequestDto {
  partyType: string;
  partyId: string;
  companyId: string;
  paymentAmount: number;
  writeOffThreshold?: number | null;
}

export interface AutoAllocationResultDto {
  allocations?: AllocationSuggestionDto[];
  totalAllocated?: number;
  unallocatedAmount?: number;
  writeOffAmount?: number;
  invoiceCount?: number;
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

export interface BankAccountDto extends EntityDto<string> {
  companyId?: string;
  accountName?: string;
  accountId?: string;
  bankName?: string;
  bankAccountNo?: string | null;
  iban?: string | null;
  swiftCode?: string | null;
  branchCode?: string | null;
  isCompanyAccount?: boolean;
  isDefault?: boolean;
  partyType?: string | null;
  partyId?: string | null;
  currencyCode?: string;
  isDisabled?: boolean;
  isCreditCard?: boolean;
  integrationId?: string | null;
  lastIntegrationDate?: string | null;
}

export interface BankAccountBalanceDto extends FullAuditedEntityDto<string> {
  bankAccountId?: string;
  bankAccountName?: string | null;
  companyId?: string | null;
  companyName?: string | null;
  date?: string;
  balance?: number;
}

export interface CreateUpdateBankAccountBalanceDto {
  bankAccountId: string;
  date: string;
  balance: number;
}

export interface GetBankAccountBalanceListDto extends PagedAndSortedResultRequestDto {
  bankAccountId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface BankClearanceDocRefDto {
  documentType: string;
  documentId: string;
}

export interface BankClearanceEntryDto {
  documentType?: string;
  documentId?: string;
  documentNumber?: string;
  postingDate?: string;
  debit?: number;
  credit?: number;
  referenceNumber?: string | null;
  clearanceDate?: string | null;
}

export interface BankGuaranteeDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  bgType?: BankGuaranteeType;
  referenceDocType?: string | null;
  referenceDocId?: string | null;
  referenceDocName?: string | null;
  customerId?: string | null;
  customerName?: string | null;
  supplierId?: string | null;
  supplierName?: string | null;
  projectId?: string | null;
  projectName?: string | null;
  amount?: number;
  startDate?: string;
  validityDays?: number;
  endDate?: string | null;
  bank?: string | null;
  bankAccountId?: string | null;
  bankAccountNumber?: string | null;
  account?: string | null;
  iban?: string | null;
  branchCode?: string | null;
  swiftNumber?: string | null;
  bankGuaranteeNumber?: string | null;
  nameOfBeneficiary?: string | null;
  marginMoney?: number;
  charges?: number;
  fixedDepositNumber?: string | null;
  clausesAndConditions?: string | null;
  status?: DocumentStatus;
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

export interface BulkClearanceResultDto {
  updatedCount?: number;
}

export interface CalculateDiscountingDto {
  totalOutstanding?: number;
  annualDiscountRate?: number;
  daysToMaturity?: number;
}

export interface CashFlowForecastDto {
  asOfDate?: string;
  forecastDays?: number;
  currentCashBalance?: number;
  totalExpectedInflows?: number;
  totalExpectedOutflows?: number;
  netCashFlow?: number;
  projectedClosingBalance?: number;
  periods?: CashFlowForecastPeriodDto[];
  upcomingInflows?: CashFlowForecastEntryDto[];
  upcomingOutflows?: CashFlowForecastEntryDto[];
  summary?: CashFlowForecastSummaryDto;
}

export interface CashFlowForecastEntryDto {
  documentId?: string;
  documentNumber?: string;
  documentType?: string;
  partyName?: string;
  dueDate?: string;
  amount?: number;
  daysUntilDue?: number;
  isOverdue?: boolean;
}

export interface CashFlowForecastPeriodDto {
  label?: string;
  periodStart?: string;
  periodEnd?: string;
  inflows?: number;
  outflows?: number;
  netFlow?: number;
  cumulativeBalance?: number;
}

export interface CashFlowForecastRequestDto {
  companyId?: string;
  asOfDate?: string | null;
  forecastDays?: number;
}

export interface CashFlowForecastSummaryDto {
  overdueReceivablesCount?: number;
  overdueReceivablesAmount?: number;
  overduePayablesCount?: number;
  overduePayablesAmount?: number;
  cashRunwayDays?: number;
  projectedCashCrunchDate?: string | null;
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

export interface ClosingBalanceStatusDto {
  latestPeriod?: string | null;
  latestClosingDate?: string | null;
  totalBalances?: number;
  totalDebit?: number;
  totalCredit?: number;
  isBalanced?: boolean;
}

export interface CoaImportResultDto {
  accountsCreated?: number;
  companyId?: string;
}

export interface CoaTemplateRowDto {
  accountCode?: string;
  accountName?: string;
  accountType?: AccountType;
  isGroup?: boolean;
  parentCode?: string | null;
  subType?: AccountSubType | null;
}

export interface CompanyReferenceDto {
  id?: string;
  name?: string;
}

export interface CostCenterAllocationDto extends EntityDto<string> {
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
  postingDate?: string;
  shortTermLoanAccountId?: string;
  bankAccountId?: string;
  bankChargesAccountId?: string;
  accountsReceivableCreditAccountId?: string;
  accountsReceivableDiscountedAccountId?: string;
  accountsReceivableUnpaidAccountId?: string;
  invoices?: CreateInvoiceDiscountingInvoiceDto[];
}

export interface CreateInvoiceDiscountingInvoiceDto {
  salesInvoiceId?: string;
  outstandingAmount?: number;
}

export interface CreateJournalEntryDto {
  companyId: string;
  fiscalYearId: string;
  postingDate: string;
  voucherType?: JournalEntryVoucherType;
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

export interface CreateJournalEntryTemplateLineDto {
  accountId: string;
  isDebit?: boolean;
  defaultAmount?: number;
  partyType?: string | null;
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

export interface CreatePartyLinkDto {
  primaryPartyType?: string;
  primaryPartyId?: string;
  secondaryPartyType?: string;
  secondaryPartyId?: string;
}

export interface CreatePaymentEntryDto {
  companyId: string;
  paymentType: PaymentType;
  postingDate: string;
  paidAmount: number;
  receivedAmount?: number | null;
  paidFromAccountId: string;
  paidToAccountId: string;
  modeOfPayment?: string | null;
  partyType?: string | null;
  partyId?: string | null;
  costCenterId?: string | null;
  projectId?: string | null;
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

export interface CreatePaymentOrderDto {
  companyId: string;
  paymentOrderType?: PaymentOrderType;
  postingDate?: string;
  partyId?: string | null;
  companyBankAccountId: string;
  references?: CreatePaymentOrderReferenceDto[];
}

export interface CreatePaymentOrderReferenceDto {
  referenceType: string;
  referenceId: string;
  amount?: number;
  supplierId?: string | null;
  modeOfPayment?: string | null;
  bankAccountId: string;
  paymentReference?: string | null;
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

export interface CreateProcessPaymentReconciliationDto {
  companyId?: string;
  partyType?: string;
  partyId?: string;
  receivablePayableAccountId?: string;
  defaultAdvanceAccountId?: string | null;
}

export interface CreateRepostAccountingLedgerDto {
  companyId?: string;
  vouchers?: RepostAccountingLedgerVoucherInputDto[];
}

export interface CreateRevaluationDto {
  companyId?: string;
  postingDate?: string;
  roundingLossAllowance?: number;
}

export interface CreateUnreconcilePaymentDto {
  companyId: string;
  voucherType?: UnreconcileVoucherType;
  voucherId: string;
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

export interface CreateUpdateBankAccountDto {
  companyId?: string;
  accountName?: string;
  accountId?: string;
  bankName?: string;
  bankAccountNo?: string | null;
  iban?: string | null;
  swiftCode?: string | null;
  branchCode?: string | null;
  isCompanyAccount?: boolean;
  partyType?: string | null;
  partyId?: string | null;
  currencyCode?: string;
  isCreditCard?: boolean;
}

export interface CreateUpdateBankGuaranteeDto {
  companyId: string;
  bgType: BankGuaranteeType;
  referenceDocType?: string | null;
  referenceDocId?: string | null;
  referenceDocName?: string | null;
  customerId?: string | null;
  customerName?: string | null;
  supplierId?: string | null;
  supplierName?: string | null;
  projectId?: string | null;
  projectName?: string | null;
  amount?: number;
  startDate: string;
  validityDays?: number;
  bank?: string | null;
  bankAccountId?: string | null;
  bankAccountNumber?: string | null;
  account?: string | null;
  iban?: string | null;
  branchCode?: string | null;
  swiftNumber?: string | null;
  bankGuaranteeNumber?: string | null;
  nameOfBeneficiary?: string | null;
  marginMoney?: number;
  charges?: number;
  fixedDepositNumber?: string | null;
  clausesAndConditions?: string | null;
}

export interface CreateUpdateJournalEntryTemplateDto {
  companyId: string;
  templateName: string;
  voucherType?: JournalEntryVoucherType;
  isActive?: boolean;
  lines?: CreateJournalEntryTemplateLineDto[];
}

export interface CreateUpdateModeOfPaymentDto {
  name?: string;
  type?: string;
  isActive?: boolean;
  defaultAccountId?: string | null;
  companyId?: string | null;
}

export interface CreateUpdateMonthlyDistributionDto {
  distributionName?: string;
  fiscalYearId?: string | null;
  percentages?: MonthlyDistributionPercentageDto[];
}

export interface CreateUpdatePaymentTermsTemplateDto {
  name?: string;
  terms?: CreatePaymentTermDto[];
}

export interface CreateUpdateShareTransferDto {
  companyId?: string;
  transferType?: ShareTransferType;
  date?: string;
  fromShareholderId?: string | null;
  toShareholderId?: string | null;
  shareTypeId?: string;
  fromNo?: number;
  toNo?: number;
  rate?: number;
  equityOrLiabilityAccountId?: string;
  assetAccountId?: string | null;
  remarks?: string | null;
}

export interface CreateUpdateShareTypeDto {
  title?: string;
  description?: string | null;
}

export interface CreateUpdateShareholderDto {
  companyId?: string;
  title?: string;
  folioNo?: string | null;
}

export interface CurrencyExchangeDto extends EntityDto<string> {
  fromCurrency?: string;
  toCurrency?: string;
  exchangeRate?: number;
  date?: string;
}

export interface DisburseInvoiceDiscountingDto {
  bankCharges?: number;
}

export interface DiscountingCalculationResultDto {
  discountCharge?: number;
  disbursementAmount?: number;
  effectiveRate?: number;
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

export interface ExchangeRateResultDto {
  rate?: number;
  fromCurrency?: string;
  toCurrency?: string;
  rateDate?: string | null;
}

export interface ExchangeRateRevaluationDto extends EntityDto<string> {
  companyId?: string;
  postingDate?: string;
  totalGainLoss?: number;
  entryCount?: number;
}

export interface ExcludedInvoiceDto {
  invoiceId?: string;
  invoiceNumber?: string | null;
  reason?: string;
}

export interface ExecuteReportDto {
  templateId?: string;
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  financeBook?: string | null;
}

export interface FinanceBookDto extends EntityDto<string> {
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

export interface FinancialReportTemplateDto extends EntityDto<string> {
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

export interface GetBankAccountListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
  isCompanyAccount?: boolean | null;
}

export interface GetBankClearanceEntriesInput {
  bankAccountId: string;
  companyId: string;
  fromDate: string;
  toDate: string;
  includeCleared?: boolean;
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

export interface GetJournalEntryTemplateListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
}

export interface GetLedgerHealthMonitorSettingsInput {
  companyId?: string;
}

export interface GetLedgerHealthRecordsInput extends PagedAndSortedResultRequestDto {
  companyId?: string;
}

export interface GetOutstandingForBatchDto {
  companyId?: string;
  partyType?: string;
  partyId?: string;
}

export interface GetUnreconcilePaymentListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
}

export interface GetUpcomingPaymentsDueInput {
  companyId?: string;
  daysAhead?: number;
  supplierId?: string | null;
}

export interface GlRepostResultDto {
  successCount?: number;
  skippedCount?: number;
  failedCount?: number;
  totalProcessed?: number;
  hasErrors?: boolean;
  errors?: string[];
}

export interface ImportBankTransactionDto {
  companyId: string;
  bankAccountId: string;
  transactionDate: string;
  description: string;
  amount: number;
  referenceNumber?: string | null;
}

export interface ImportCoaDto {
  companyId?: string;
  rows?: ImportCoaRowDto[];
}

export interface ImportCoaRowDto {
  accountCode?: string;
  accountName?: string;
  accountType?: AccountType;
  isGroup?: boolean;
  parentCode?: string | null;
  subType?: AccountSubType | null;
}

export interface InternalTransferResultDto {
  paymentEntryId?: string;
  paymentNumber?: string | null;
  sourceTransactionId?: string;
  mirrorTransactionId?: string | null;
}

export interface InvoiceDiscountingDto extends EntityDto<string> {
  companyId?: string;
  postingDate?: string;
  loanStartDate?: string | null;
  loanPeriodDays?: number;
  loanEndDate?: string | null;
  status?: number;
  totalAmount?: number;
  bankCharges?: number;
  shortTermLoanAccountId?: string;
  bankAccountId?: string;
  bankChargesAccountId?: string;
  accountsReceivableCreditAccountId?: string;
  accountsReceivableDiscountedAccountId?: string;
  accountsReceivableUnpaidAccountId?: string;
  sanctionJournalEntryId?: string | null;
  disbursementJournalEntryId?: string | null;
  settlementJournalEntryId?: string | null;
  invoices?: InvoiceDiscountingInvoiceDto[];
}

export interface InvoiceDiscountingInvoiceDto {
  salesInvoiceId?: string;
  invoiceNumber?: string | null;
  customerId?: string;
  customerName?: string | null;
  outstandingAmount?: number;
}

export interface InvoiceForDiscountingDto {
  invoiceId?: string;
  invoiceNumber?: string;
  customerId?: string;
  customerName?: string;
  issueDate?: string;
  outstandingAmount?: number;
}

export interface JournalEntryDto extends EntityDto<string> {
  companyId?: string;
  fiscalYearId?: string;
  entryNumber?: string | null;
  postingDate?: string;
  voucherType?: JournalEntryVoucherType;
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

export interface JournalEntryTemplateDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  templateName?: string;
  voucherType?: JournalEntryVoucherType;
  isActive?: boolean;
  lines?: JournalEntryTemplateLineDto[];
}

export interface JournalEntryTemplateLineDto {
  id?: string;
  accountId?: string;
  accountCode?: string | null;
  accountName?: string | null;
  isDebit?: boolean;
  defaultAmount?: number;
  partyType?: string | null;
  description?: string | null;
}

export interface LedgerHealthCheckRunResultDto {
  isHealthy?: boolean;
  totalChecked?: number;
  issues?: LedgerHealthRecordDto[];
}

export interface LedgerHealthMonitorSettingsDto {
  companyId?: string;
  isEnabled?: boolean;
  lookbackPeriodDays?: number;
}

export interface LedgerHealthRecordDto extends EntityDto<string> {
  checkType?: string;
  severity?: string;
  description?: string;
  voucherType?: string | null;
  voucherId?: string | null;
  difference?: number | null;
  checkedAt?: string;
}

export interface MakePaymentRecordsDto {
  supplierId: string;
  modeOfPayment?: string | null;
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
  isActive?: boolean;
  defaultAccountId?: string | null;
  companyId?: string | null;
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

export interface MonthlyDistributionDto extends EntityDto<string> {
  distributionName?: string;
  fiscalYearId?: string | null;
  percentages?: MonthlyDistributionPercentageDto[];
}

export interface MonthlyDistributionPercentageDto {
  month?: number;
  percentageAllocation?: number;
}

export interface MonthlyProfitLossReportDto {
  year?: number;
  companyId?: string;
  monthLabels?: string[];
  revenueRows?: MonthlyProfitLossRowDto[];
  expenseRows?: MonthlyProfitLossRowDto[];
  monthlyRevenue?: number[];
  monthlyExpense?: number[];
  monthlyNetProfit?: number[];
  annualRevenue?: number;
  annualExpense?: number;
  annualNetProfit?: number;
}

export interface MonthlyProfitLossRequestDto {
  companyId: string;
  year: number;
  startMonth?: number;
}

export interface MonthlyProfitLossRowDto {
  accountId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: string;
  monthlyAmounts?: number[];
  annualTotal?: number;
}

export interface Mt940ImportInput {
  companyId?: string;
  bankAccountId?: string;
  mt940Content?: string;
  tenantId?: string | null;
  currencyCode?: string | null;
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
  daysOverdue?: number;
  isOverdue?: boolean;
}

export interface OutstandingOrderForPaymentDto {
  orderId?: string;
  orderNumber?: string;
  orderDate?: string;
  grandTotal?: number;
  advancePaid?: number;
  pendingAdvance?: number;
  currencyCode?: string;
  orderType?: string;
  partyName?: string | null;
}

export interface PartyDashboardDto {
  ytdBilling?: number;
  totalUnpaid?: number;
  loyaltyPoints?: number;
  companies?: CompanyReferenceDto[];
}

export interface PartyLinkDto extends EntityDto<string> {
  primaryPartyType?: string;
  primaryPartyId?: string;
  secondaryPartyType?: string;
  secondaryPartyId?: string;
}

export interface PartyOutstandingDto {
  invoices?: OutstandingInvoiceForPaymentDto[];
  orders?: OutstandingOrderForPaymentDto[];
  totalInvoiceOutstanding?: number;
  totalOrderPending?: number;
}

export interface PayableInvoiceInfoDto {
  invoiceId?: string;
  invoiceNumber?: string;
  supplierId?: string;
  partyAccountId?: string;
  outstanding?: number;
  currencyCode?: string;
}

export interface PayableInvoicePartitionDto {
  payable?: PayableInvoiceInfoDto[];
  excluded?: ExcludedInvoiceDto[];
  totalPayable?: number;
  paymentEntryCount?: number;
}

export interface PaymentEntryDto extends EntityDto<string> {
  companyId?: string;
  paymentNumber?: string | null;
  paymentType?: string;
  postingDate?: string;
  modeOfPayment?: string | null;
  paidAmount?: number;
  receivedAmount?: number;
  currencyCode?: string;
  status?: string;
  referenceNumber?: string | null;
  partyType?: string | null;
  partyId?: string | null;
  partyName?: string | null;
}

export interface PaymentLedgerRepostResultDto {
  totalVouchers?: number;
  successCount?: number;
  failedCount?: number;
  hasErrors?: boolean;
  errors?: string[];
}

export interface PaymentOrderDto extends AuditedEntityDto<string> {
  companyId?: string;
  orderNumber?: string | null;
  paymentOrderType?: PaymentOrderType;
  postingDate?: string;
  partyId?: string | null;
  companyBankAccountId?: string;
  status?: number;
  amendedFromId?: string | null;
  references?: PaymentOrderReferenceDto[];
}

export interface PaymentOrderReferenceDto {
  id?: string;
  referenceType?: string;
  referenceId?: string;
  amount?: number;
  supplierId?: string | null;
  modeOfPayment?: string | null;
  bankAccountId?: string;
  paymentReference?: string | null;
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

export interface PcvGlEntryDto {
  accountId?: string;
  accountName?: string | null;
  debit?: number;
  credit?: number;
  costCenterId?: string | null;
  postingDate?: string;
}

export interface PeriodClosingVoucherDto extends EntityDto<string> {
  companyId?: string;
  fiscalYearId?: string;
  voucherNumber?: string | null;
  postingDate?: string;
  transactionDate?: string;
  closingAccountId?: string;
  closingAccountName?: string | null;
  totalClosingAmount?: number;
  status?: number;
  remarks?: string | null;
  entryCount?: number;
}

export interface ProcessPaymentReconciliationDto extends EntityDto<string> {
  companyId?: string;
  partyType?: string;
  partyId?: string;
  receivablePayableAccountId?: string;
  defaultAdvanceAccountId?: string | null;
  status?: number;
  statusName?: string;
  reconciledCount?: number;
  errorLog?: string | null;
  creationTime?: string;
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
  previousTotalRevenue?: number | null;
  previousTotalExpense?: number | null;
  previousNetProfitOrLoss?: number | null;
  previousFromDate?: string | null;
  previousToDate?: string | null;
}

export interface ProfitLossRequestDto {
  companyId: string;
  fromDate: string;
  toDate: string;
  includeComparison?: boolean;
}

export interface ProfitLossRowDto {
  accountId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: string;
  amount?: number;
  previousPeriodAmount?: number | null;
  growthPercentage?: number | null;
  level?: number;
  isGroup?: boolean;
}

export interface RebuildClosingBalanceDto {
  companyId?: string;
  closingDate?: string;
  period?: string;
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

export interface RepostAccountingLedgerDto extends EntityDto<string> {
  companyId?: string;
  status?: number;
  statusName?: string;
  errorLog?: string | null;
  creationTime?: string;
  vouchers?: RepostAccountingLedgerVoucherDto[];
}

export interface RepostAccountingLedgerVoucherDto extends EntityDto<string> {
  voucherType?: string;
  voucherId?: string;
  voucherNumber?: string;
  status?: number;
  statusName?: string;
  errorMessage?: string | null;
}

export interface RepostAccountingLedgerVoucherInputDto {
  voucherType?: string;
  voucherId?: string;
}

export interface RepostBatchGlDto {
  companyId?: string;
  vouchers?: RepostVoucherRefDto[];
}

export interface RepostGlDto {
  companyId?: string;
  voucherType?: string;
  voucherId?: string;
}

export interface RepostPaymentLedgerDto {
  companyId?: string;
  voucherType?: string;
  voucherId?: string;
}

export interface RepostPaymentLedgerForCompanyDto {
  companyId?: string;
  fromDate?: string;
}

export interface RepostVoucherRefDto {
  voucherType?: string;
  voucherId?: string;
}

export interface RepostableVoucherDto {
  voucherType?: string;
  voucherId?: string;
  voucherNumber?: string;
  postingDate?: string;
}

export interface RunLedgerHealthCheckDto {
  companyId?: string;
}

export interface SendPaymentReminderInput {
  partyId?: string;
  partyName?: string;
  partyType?: string;
  overdueAmount?: number;
  invoiceCount?: number;
}

export interface SetClearanceDateDto {
  entries: BankClearanceDocRefDto[];
  clearanceDate?: string | null;
}

export interface ShareBalanceEntryDto {
  shareTypeId?: string;
  fromNo?: number;
  toNo?: number;
  noOfShares?: number;
  rate?: number;
  amount?: number;
  isCompany?: boolean;
  currentState?: string | null;
}

export interface ShareTransferDto extends EntityDto<string> {
  companyId?: string;
  transferType?: number;
  date?: string;
  fromShareholderId?: string | null;
  fromFolioNo?: string | null;
  toShareholderId?: string | null;
  toFolioNo?: string | null;
  shareTypeId?: string;
  fromNo?: number;
  toNo?: number;
  noOfShares?: number;
  rate?: number;
  amount?: number;
  equityOrLiabilityAccountId?: string;
  assetAccountId?: string | null;
  remarks?: string | null;
  status?: number;
}

export interface ShareTypeDto extends EntityDto<string> {
  title?: string;
  description?: string | null;
}

export interface ShareholderDto extends EntityDto<string> {
  companyId?: string;
  title?: string;
  folioNo?: string | null;
  isCompany?: boolean;
  shareBalances?: ShareBalanceEntryDto[];
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

export interface SubmitInvoiceDiscountingDto {
  loanStartDate?: string;
  loanPeriodDays?: number;
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
  includeSubsidiaries?: boolean;
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

export interface UnreconcilePaymentAllocationDto {
  id?: string;
  paymentLedgerEntryId?: string;
  againstVoucherType?: string;
  againstVoucherId?: string;
  amount?: number;
  unlinked?: boolean;
}

export interface UnreconcilePaymentDto extends AuditedEntityDto<string> {
  companyId?: string;
  voucherType?: UnreconcileVoucherType;
  voucherId?: string;
  status?: number;
  allocations?: UnreconcilePaymentAllocationDto[];
}

export interface UnreconciledPaymentDto {
  voucherId?: string;
  voucherType?: string;
  documentNumber?: string | null;
  postingDate?: string;
  totalAmount?: number;
  unallocatedAmount?: number;
  currencyCode?: string;
  exchangeRate?: number;
}

export interface UpcomingPaymentDueDto {
  invoiceId?: string;
  invoiceNumber?: string;
  supplierId?: string;
  supplierName?: string;
  dueDate?: string;
  outstandingAmount?: number;
  grandTotal?: number;
  currencyCode?: string | null;
  daysUntilDue?: number;
  weekLabel?: string;
  isOverdue?: boolean;
}

export interface UpcomingPaymentsDueReportDto {
  totalDueThisWeek?: number;
  totalDueNextWeek?: number;
  totalDueNext30Days?: number;
  totalOverdue?: number;
  invoiceCount?: number;
  supplierCount?: number;
  invoices?: UpcomingPaymentDueDto[];
}

export interface UpdateAccountingDimensionDto {
  label: string;
  isMandatory?: boolean;
  hideDisabledValues?: boolean;
  companyId?: string | null;
}

export interface UpdateLedgerHealthMonitorSettingsDto {
  companyId?: string;
  isEnabled?: boolean;
  lookbackPeriodDays?: number;
}

export interface ValidatePayableInvoicesDto {
  invoiceIds: string[];
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

export interface BankAccountTypeDto extends FullAuditedEntityDto<string> {
  accountTypeName: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateUpdateBankAccountTypeDto {
  accountTypeName: string;
  description?: string | null;
  isActive?: boolean;
}

export interface GetBankAccountTypeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  isActive?: boolean | null;
}

export interface BankAccountSubtypeDto extends FullAuditedEntityDto<string> {
  accountSubtypeName: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateUpdateBankAccountSubtypeDto {
  accountSubtypeName: string;
  description?: string | null;
  isActive?: boolean;
}

export interface GetBankAccountSubtypeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  isActive?: boolean | null;
}

export interface BankDto extends FullAuditedEntityDto<string> {
  bankName: string;
  swiftNumber?: string | null;
  website?: string | null;
  isActive: boolean;
}

export interface CreateUpdateBankDto {
  bankName: string;
  swiftNumber?: string | null;
  website?: string | null;
  isActive?: boolean;
}

export interface GetBankListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  isActive?: boolean | null;
}

export interface ChequePrintTemplateDto extends FullAuditedEntityDto<string> {
  bankName: string;
  chequeSize: ChequeSize;
  startingPositionFromTopEdge: number;
  chequeWidth: number;
  chequeHeight: number;
  scannedCheque?: string | null;
  isAccountPayable: boolean;
  accPayDistFromTopEdge: number;
  accPayDistFromLeftEdge: number;
  messageToShow?: string | null;
  dateDistFromTopEdge: number;
  dateDistFromLeftEdge: number;
  payerNameFromTopEdge: number;
  payerNameFromLeftEdge: number;
  amtInWordsFromTopEdge: number;
  amtInWordsFromLeftEdge: number;
  amtInWordWidth: number;
  amtInWordsLineSpacing: number;
  amtInFiguresFromTopEdge: number;
  amtInFiguresFromLeftEdge: number;
  accNoDistFromTopEdge: number;
  accNoDistFromLeftEdge: number;
  signatoryFromTopEdge: number;
  signatoryFromLeftEdge: number;
  hasPrintFormat: boolean;
}

export interface CreateUpdateChequePrintTemplateDto {
  bankName: string;
  chequeSize?: ChequeSize;
  startingPositionFromTopEdge?: number;
  chequeWidth?: number;
  chequeHeight?: number;
  scannedCheque?: string | null;
  isAccountPayable?: boolean;
  accPayDistFromTopEdge?: number;
  accPayDistFromLeftEdge?: number;
  messageToShow?: string | null;
  dateDistFromTopEdge?: number;
  dateDistFromLeftEdge?: number;
  payerNameFromTopEdge?: number;
  payerNameFromLeftEdge?: number;
  amtInWordsFromTopEdge?: number;
  amtInWordsFromLeftEdge?: number;
  amtInWordWidth?: number;
  amtInWordsLineSpacing?: number;
  amtInFiguresFromTopEdge?: number;
  amtInFiguresFromLeftEdge?: number;
  accNoDistFromTopEdge?: number;
  accNoDistFromLeftEdge?: number;
  signatoryFromTopEdge?: number;
  signatoryFromLeftEdge?: number;
  hasPrintFormat?: boolean;
}

export interface GetChequePrintTemplateListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface ChequePrintPreviewDto {
  htmlContent: string;
}

export interface SubscriptionSettingsDto extends FullAuditedEntityDto<string> {
  gracePeriod: number;
  cancelAfterGrace: boolean;
  prorate: boolean;
}

export interface UpdateSubscriptionSettingsDto {
  gracePeriod?: number;
  cancelAfterGrace?: boolean;
  prorate?: boolean;
}

