import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ValuationMethod } from '../inventory/valuation-method.enum';
import type { AuthorizationBasedOn } from './authorization-based-on.enum';
import type { RepeatFrequency } from './repeat-frequency.enum';
import type { RepeatDayOfWeek } from './repeat-day-of-week.enum';
import type { EmailDigestFrequency } from './email-digest-frequency.enum';
import type { DocumentStatus } from './document-status.enum';

export interface AddressDto extends EntityDto<string> {
  title?: string;
  addressType?: string;
  addressLine1?: string;
  addressLine2?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string;
  phone?: string | null;
  email?: string | null;
  partyType?: string;
  partyId?: string;
  isPrimaryAddress?: boolean;
  isShippingAddress?: boolean;
}

export interface AgingBucketsDto {
  current?: number;
  thirtyOneToSixty?: number;
  sixtyOneToNinety?: number;
  ninetyPlus?: number;
  total?: number;
}

export interface AgingSummaryWidgetDto {
  receivables?: AgingBucketsDto;
  payables?: AgingBucketsDto;
}

export interface AuthorizationRuleDto extends EntityDto<string> {
  companyId?: string | null;
  transactionType?: string;
  basedOn?: string;
  thresholdValue?: number;
  systemUserId?: string | null;
  systemRole?: string | null;
  approvingRole?: string | null;
  approvingUserId?: string | null;
  customerId?: string | null;
}

export interface AutoRepeatDto extends EntityDto<string> {
  companyId?: string;
  referenceDocumentType?: string;
  referenceDocumentId?: string;
  referenceDocumentNumber?: string | null;
  frequency?: string;
  startDate?: string;
  endDate?: string | null;
  nextScheduleDate?: string;
  isEnabled?: boolean;
  generatedCount?: number;
  lastGeneratedDate?: string | null;
  notifyByEmail?: boolean;
}

export interface BankAccountBalanceDto {
  accountName?: string;
  accountCode?: string;
  balance?: number;
  accountType?: string;
}

export interface BankBalanceWidgetDto {
  totalCashAndBank?: number;
  accounts?: BankAccountBalanceDto[];
}

export interface BranchDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  name?: string;
  code?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  isActive?: boolean;
  isHeadquarters?: boolean;
}

export interface CashFlowSnapshotDto {
  expectedInflows30Days?: number;
  expectedOutflows30Days?: number;
  netCashFlow30Days?: number;
  inflowInvoiceCount?: number;
  outflowInvoiceCount?: number;
  overdueReceivables?: number;
  overduePayables?: number;
  overdueReceivableCount?: number;
  overduePayableCount?: number;
}

export interface CompanyDto extends FullAuditedEntityDto<string> {
  name?: string;
  shortName?: string | null;
  taxId?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  msicCode?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  currencyCode?: string;
  fiscalYearStartMonth?: number;
  isActive?: boolean;
  stockFrozenUpto?: string | null;
  accountsFrozenTillDate?: string | null;
  enablePerpetualInventory?: boolean;
  defaultValuationMethod?: ValuationMethod | null;
  overDeliveryReceiptAllowance?: number;
  overBillingAllowance?: number;
  allowUomWithConversionRateDefinedInItem?: boolean;
  defaultWarehouseId?: string | null;
  sampleRetentionWarehouseId?: string | null;
  defaultInTransitWarehouseId?: string | null;
  defaultWarehouseForSalesReturnId?: string | null;
  defaultWipWarehouseId?: string | null;
  defaultFgWarehouseId?: string | null;
  defaultScrapWarehouseId?: string | null;
  defaultReceivableAccountId?: string | null;
  defaultPayableAccountId?: string | null;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  defaultBankAccountId?: string | null;
  defaultInventoryAccountId?: string | null;
  stockReceivedButNotBilledAccountId?: string | null;
  stockDeliveredButNotBilledAccountId?: string | null;
  depreciationExpenseAccountId?: string | null;
  accumulatedDepreciationAccountId?: string | null;
  exchangeGainLossAccountId?: string | null;
  defaultCostCenterId?: string | null;
  roundOffAccountId?: string | null;
  roundOffForOpeningAccountId?: string | null;
  bookAdvancePaymentsInSeparatePartyAccount?: boolean;
  defaultAdvanceReceivedAccountId?: string | null;
  defaultAdvancePaidAccountId?: string | null;
}

export interface CompanyRestrictionDto {
  parentType?: string;
  parentId?: string;
  restrictToCompanies?: boolean;
  allowedCompanies?: CompanyRestrictionEntryDto[];
}

export interface CompanyRestrictionEntryDto {
  id?: string;
  companyId?: string;
}

export interface ConnectionDocumentDto {
  id?: string;
  documentNumber?: string | null;
  status?: string | null;
  amount?: number | null;
  date?: string | null;
  route?: string;
}

export interface ConnectionGroupDto {
  label?: string;
  items?: ConnectionItemDto[];
}

export interface ConnectionItemDto {
  documentType?: string;
  count?: number;
  route?: string;
  documents?: ConnectionDocumentDto[];
}

export interface ContactDto extends EntityDto<string> {
  partyType?: string;
  partyId?: string;
  firstName?: string;
  lastName?: string | null;
  salutation?: string | null;
  fullName?: string;
  email?: string | null;
  phone?: string | null;
  mobileNo?: string | null;
  designation?: string | null;
  department?: string | null;
  isPrimaryContact?: boolean;
  isBillingContact?: boolean;
}

export interface CostCenterLookupDto {
  id?: string;
  name?: string;
  isGroup?: boolean;
  parentId?: string | null;
}

export interface CreateAuthorizationRuleDto {
  companyId?: string | null;
  transactionType?: string;
  basedOn?: AuthorizationBasedOn;
  thresholdValue?: number;
  systemUserId?: string | null;
  systemRole?: string | null;
  approvingRole?: string | null;
  approvingUserId?: string | null;
  customerId?: string | null;
}

export interface CreateAutoRepeatDto {
  companyId?: string;
  referenceDocumentType?: string;
  referenceDocumentId?: string;
  referenceDocumentNumber?: string | null;
  frequency?: RepeatFrequency;
  dayOfWeek?: RepeatDayOfWeek | null;
  dayOfMonth?: number | null;
  startDate?: string;
  endDate?: string | null;
  notifyByEmail?: boolean;
  notifyRecipients?: string | null;
}

export interface CreateContactDto {
  partyType?: string;
  partyId?: string;
  salutation?: string | null;
  firstName?: string;
  lastName?: string | null;
  email?: string | null;
  phone?: string | null;
  mobileNo?: string | null;
  designation?: string | null;
  department?: string | null;
  isPrimaryContact?: boolean;
  isBillingContact?: boolean;
}

export interface CreateDocumentSeriesDto {
  companyId?: string;
  name?: string;
  documentType?: string;
  prefix?: string;
  numberPadding?: number;
}

export interface CreateEmailTemplateDto {
  name?: string;
  subject?: string;
  body?: string;
  documentType?: string | null;
}

export interface CreateHierarchyNodeDto {
  name?: string;
  parentId?: string | null;
  isGroup?: boolean;
  managerId?: string | null;
}

export interface CreateUpdateAddressDto {
  title: string;
  addressType?: string | null;
  addressLine1: string;
  addressLine2?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country: string;
  phone?: string | null;
  email?: string | null;
  partyType: string;
  partyId: string;
  isPrimaryAddress?: boolean;
  isShippingAddress?: boolean;
}

export interface CreateUpdateBranchDto {
  companyId: string;
  name: string;
  code?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  isActive?: boolean;
  isHeadquarters?: boolean;
}

export interface CreateUpdateCompanyDto {
  name: string;
  shortName?: string | null;
  taxId?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  msicCode?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  currencyCode: string;
  fiscalYearStartMonth?: number;
  isActive?: boolean;
  allowUomWithConversionRateDefinedInItem?: boolean;
  defaultWarehouseId?: string | null;
  sampleRetentionWarehouseId?: string | null;
  defaultInTransitWarehouseId?: string | null;
  defaultWarehouseForSalesReturnId?: string | null;
  defaultWipWarehouseId?: string | null;
  defaultFgWarehouseId?: string | null;
  defaultScrapWarehouseId?: string | null;
  defaultReceivableAccountId?: string | null;
  defaultPayableAccountId?: string | null;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  defaultBankAccountId?: string | null;
  defaultInventoryAccountId?: string | null;
  stockReceivedButNotBilledAccountId?: string | null;
  stockDeliveredButNotBilledAccountId?: string | null;
  defaultCostCenterId?: string | null;
  roundOffAccountId?: string | null;
  roundOffForOpeningAccountId?: string | null;
  bookAdvancePaymentsInSeparatePartyAccount?: boolean;
  defaultAdvanceReceivedAccountId?: string | null;
  defaultAdvancePaidAccountId?: string | null;
}

export interface CustomerPerformanceDto {
  totalRevenue?: number;
  revenueThisMonth?: number;
  revenueLastMonth?: number;
  revenueGrowthPercent?: number;
  totalOrders?: number;
  ordersThisMonth?: number;
  averageOrderValue?: number;
  averageDaysToPayment?: number;
  onTimePaymentPercent?: number;
  overdueInvoiceCount?: number;
  totalOverdueAmount?: number;
  creditLimit?: number;
  creditUsed?: number;
  creditUtilizationPercent?: number;
  revenueTrend?: MonthlyRevenuePoint[];
}

export interface DashboardSummaryDto {
  totalCustomers?: number;
  totalSuppliers?: number;
  totalItems?: number;
  draftInvoices?: number;
  outstandingInvoices?: number;
  pendingPurchaseOrders?: number;
  submittedEInvoices?: number;
  pendingApprovals?: number;
  monthlyRevenue?: number;
  monthlyExpenses?: number;
}

export interface DeliveryDueAlertDto {
  overdueCount?: number;
  dueThisWeekCount?: number;
  dueNext7DaysCount?: number;
  overdueTotalValue?: number;
  overdueOrders?: DeliveryDueOrderDto[];
  upcomingOrders?: DeliveryDueOrderDto[];
}

export interface DeliveryDueOrderDto {
  purchaseOrderId?: string;
  orderNumber?: string;
  supplierName?: string;
  expectedDeliveryDate?: string | null;
  daysOverdue?: number;
  grandTotal?: number;
  perReceived?: number;
}

export interface DocumentActivityLogDto extends EntityDto<string> {
  documentType?: string;
  documentId?: string;
  documentNumber?: string | null;
  activityType?: string;
  previousStatus?: string | null;
  newStatus?: string | null;
  performedByUserId?: string | null;
  details?: string | null;
  creationTime?: string;
}

export interface DocumentConnectionsDto {
  groups?: ConnectionGroupDto[];
}

export interface DocumentPrintResult {
  html?: string;
  fileName?: string;
  documentType?: string;
}

export interface DocumentSeriesDto extends EntityDto<string> {
  companyId?: string;
  documentType?: string;
  prefix?: string;
  currentNumber?: number;
  numberPadding?: number;
}

export interface DraftLinkDto {
  documentId?: string;
  documentNumber?: string | null;
  documentType?: string;
  url?: string | null;
}

export interface EmailDigestSendResultDto {
  recipientCount?: number;
  openSalesOrderCount?: number;
  overdueInvoiceCount?: number;
  overdueInvoiceAmount?: number;
  lowStockItemCount?: number;
}

export interface EmailDigestSettingsDto {
  companyId?: string;
  isEnabled?: boolean;
  frequency?: EmailDigestFrequency;
  recipients?: string;
  includeOpenSalesOrders?: boolean;
  includeOverdueInvoices?: boolean;
  includeLowStockItems?: boolean;
  lastSentAt?: string | null;
}

export interface EmailTemplateDto extends EntityDto<string> {
  name?: string;
  subject?: string;
  body?: string;
  documentType?: string | null;
}

export interface ExpiringBatchDto {
  batchId?: string;
  batchNo?: string;
  itemCode?: string;
  itemName?: string;
  expiryDate?: string;
  daysUntilExpiry?: number;
  stockQty?: number;
  warehouseName?: string | null;
}

export interface ExpiringQuotationDto {
  quotationId?: string;
  quotationNumber?: string;
  customerName?: string;
  grandTotal?: number;
  validUntil?: string;
  daysRemaining?: number;
}

export interface FinancialKpiDto {
  monthlyRevenue?: number;
  monthlyExpenses?: number;
  netProfit?: number;
  profitMargin?: number;
  arOutstanding?: number;
  apOutstanding?: number;
  netCashPosition?: number;
  revenueGrowth?: number;
  invoiceCount?: number;
  billCount?: number;
  periodLabel?: string;
}

export interface GetEmailDigestSettingsInput {
  companyId?: string;
}

export interface GetNotificationLogListDto extends PagedAndSortedResultRequestDto {
  channel?: string | null;
  status?: string | null;
  documentType?: string | null;
}

export interface GetPartyDetailsInput {
  partyId?: string;
  companyId?: string | null;
}

export interface GlobalSearchInput {
  query?: string;
  companyId?: string;
  maxResults?: number;
}

export interface HierarchyNodeDto {
  id?: string;
  name?: string;
  parentId?: string | null;
  isGroup?: boolean;
}

export interface ItemGroupLookupDto {
  id?: string;
  name?: string;
  isGroup?: boolean;
  parentId?: string | null;
}

export interface LowStockItemDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  reorderLevel?: number;
  currentStock?: number;
  projectedQty?: number;
}

export interface ModeOfPaymentLookupDto {
  id?: string;
  name?: string;
  type?: string;
}

export interface MonthlyRevenuePoint {
  month?: string;
  amount?: number;
}

export interface NotificationLogDto extends EntityDto<string> {
  recipient?: string;
  subject?: string | null;
  channel?: string;
  status?: string;
  documentType?: string | null;
  documentId?: string | null;
  errorMessage?: string | null;
  retryCount?: number;
  sentAt?: string | null;
  createdAt?: string;
}

export interface OperationalMetricsDto {
  draftDocuments?: number;
  pendingApprovals?: number;
  overdueInvoices?: number;
  lowStockItems?: number;
  totalArOutstanding?: number;
  totalApOutstanding?: number;
  oldestUnpaidInvoiceDays?: number;
  activeSubscriptions?: number;
  openWorkOrders?: number;
  pendingMaterialRequests?: number;
  itemsWithoutPrice?: number;
  customersWithoutContact?: number;
  lastNightlyRunDate?: string | null;
}

export interface OverdueAlertsDto {
  overdueReceivableCount?: number;
  overdueReceivableAmount?: number;
  overduePayableCount?: number;
  overduePayableAmount?: number;
  pendingApprovalCount?: number;
  overduePurchaseOrderCount?: number;
}

export interface PartyDetailsDto {
  partyId?: string;
  partyName?: string;
  partyType?: string;
  tin?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  idType?: string | null;
  idValue?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  billingAddressId?: string | null;
  billingAddress?: string | null;
  billingCity?: string | null;
  billingState?: string | null;
  billingPostalCode?: string | null;
  billingCountry?: string | null;
  shippingAddressId?: string | null;
  shippingAddress?: string | null;
  defaultPaymentTermsTemplateId?: string | null;
  paymentTermsTemplateName?: string | null;
  defaultCreditDays?: number;
  defaultReceivableAccountId?: string | null;
  defaultPayableAccountId?: string | null;
  customerGroupId?: string | null;
  territoryId?: string | null;
  companyCurrency?: string | null;
  creditLimit?: number;
  outstanding?: number;
}

export interface PaymentTermsLookupDto {
  id?: string;
  name?: string;
}

export interface PendingMaterialRequestDto {
  id?: string;
  requestNumber?: string;
  requestDate?: string;
  status?: DocumentStatus;
  itemCount?: number;
  requiredByDate?: string | null;
}

export interface PendingOrdersSummaryDto {
  salesOrdersToDeliverAndBill?: number;
  salesOrdersToDeliver?: number;
  salesOrdersToBill?: number;
  totalActiveSalesOrders?: number;
  purchaseOrdersToReceiveAndBill?: number;
  purchaseOrdersToReceive?: number;
  purchaseOrdersToBill?: number;
  totalActivePurchaseOrders?: number;
}

export interface PoFulfillmentItemDto {
  purchaseOrderId?: string;
  orderNumber?: string;
  orderDate?: string;
  supplierName?: string;
  itemId?: string;
  itemName?: string;
  orderedQty?: number;
  receivedQty?: number;
  billedQty?: number;
  pendingReceiptQty?: number;
  pendingBillingQty?: number;
  expectedDeliveryDate?: string | null;
  isOverdue?: boolean;
  daysOverdue?: number;
  fulfillmentStatus?: string;
}

export interface PoFulfillmentReportDto {
  totalItems?: number;
  pendingReceiptItems?: number;
  pendingBillingItems?: number;
  overdueItems?: number;
  totalPendingValue?: number;
  items?: PoFulfillmentItemDto[];
}

export interface ProductionSummaryDto {
  draft?: number;
  notStarted?: number;
  inProcess?: number;
  completed?: number;
  stopped?: number;
  totalActiveOrders?: number;
  totalProducedThisMonth?: number;
}

export interface ProfitMarginTrendDto {
  month?: string;
  revenue?: number;
  cost?: number;
  grossProfit?: number;
  marginPercentage?: number;
}

export interface QuickReorderDto {
  companyId?: string;
  itemIds?: string[];
}

export interface QuickReorderResultDto {
  materialRequestId?: string;
  materialRequestNumber?: string;
  itemCount?: number;
}

export interface RenderedTemplateDto {
  subject?: string;
  body?: string;
}

export interface ReorderPointDashboardDto {
  totalItemsBelowReorder?: number;
  criticalItems?: number;
  totalShortageValue?: number;
  items?: ReorderPointItemDto[];
}

export interface ReorderPointItemDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  currentStock?: number;
  reorderLevel?: number;
  projectedQty?: number;
  shortageQty?: number;
  warehouseName?: string;
}

export interface RevenueTrendDto {
  month?: string;
  amount?: number;
}

export interface RevenueVsExpenseDto {
  month?: string;
  revenue?: number;
  expenses?: number;
  netProfit?: number;
  profitMarginPct?: number;
}

export interface SaveCompanyRestrictionDto {
  parentType?: string;
  parentId?: string;
  restrictToCompanies?: boolean;
  allowedCompanyIds?: string[] | null;
}

export interface SearchResultDto {
  id?: string;
  documentType?: string;
  documentNumber?: string;
  date?: string;
  amount?: number;
  status?: string;
  route?: string;
}

export interface SendEmailDigestNowInput {
  companyId?: string;
}

export interface StockValuationItemDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  quantity?: number;
  valuationRate?: number;
  stockValue?: number;
}

export interface StockValuationWidgetDto {
  totalStockValue?: number;
  totalItems?: number;
  totalQuantity?: number;
  topItemsByValue?: StockValuationItemDto[];
}

export interface SupplierPerformanceDto {
  totalSpend?: number;
  spendThisMonth?: number;
  spendLastMonth?: number;
  totalOrders?: number;
  ordersThisMonth?: number;
  averageOrderValue?: number;
  averageLeadTimeDays?: number;
  onTimeDeliveryPercent?: number;
  pendingReceiptCount?: number;
  totalOutstandingPayable?: number;
  overduePayableCount?: number;
  spendTrend?: MonthlyRevenuePoint[];
}

export interface SupplierPerformanceItemDto {
  supplierId?: string;
  supplierName?: string;
  totalOrders?: number;
  onTimeCount?: number;
  lateCount?: number;
  onTimeRate?: number;
  totalValue?: number;
}

export interface SupplierPerformanceWidgetDto {
  totalSuppliers?: number;
  overallOnTimeRate?: number;
  suppliersAtRisk?: number;
  suppliers?: SupplierPerformanceItemDto[];
}

export interface TodaysActivityDto {
  invoicesCreated?: number;
  paymentsReceived?: number;
  ordersPlaced?: number;
  deliveriesMade?: number;
  receiptsProcessed?: number;
  totalInvoiced?: number;
  totalCollected?: number;
}

export interface TopCustomerDto {
  customerId?: string;
  customerName?: string;
  revenue?: number;
  invoiceCount?: number;
}

export interface TopDebtorDto {
  customerId?: string;
  customerName?: string;
  totalOutstanding?: number;
  invoiceCount?: number;
  oldestDueDate?: string | null;
  daysOverdue?: number;
}

export interface UpcomingPaymentDuesDto {
  receivablesDueIn7Days?: number;
  receivablesDueIn14Days?: number;
  receivablesDueIn30Days?: number;
  receivablesOverdue?: number;
  payablesDueIn7Days?: number;
  payablesDueIn14Days?: number;
  payablesDueIn30Days?: number;
  payablesOverdue?: number;
  receivableInvoiceCount?: number;
  payableInvoiceCount?: number;
}

export interface UpdateAuthorizationRuleDto {
  thresholdValue?: number;
  systemUserId?: string | null;
  systemRole?: string | null;
  approvingRole?: string | null;
  approvingUserId?: string | null;
  customerId?: string | null;
}

export interface UpdateCompanySettingsDto {
  defaultCurrency?: string | null;
  fiscalYearStartMonth?: number | null;
  stockFrozenUpto?: string | null;
  accountsFrozenTillDate?: string | null;
  defaultValuationMethod?: string | null;
  enablePerpetualInventory?: boolean;
  overDeliveryAllowance?: number;
  overBillingAllowance?: number;
  allowUomWithConversionRateDefinedInItem?: boolean;
  defaultReceivableAccountId?: string | null;
  defaultPayableAccountId?: string | null;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  defaultBankAccountId?: string | null;
  defaultInventoryAccountId?: string | null;
  depreciationExpenseAccountId?: string | null;
  accumulatedDepreciationAccountId?: string | null;
  exchangeGainLossAccountId?: string | null;
  defaultCostCenterId?: string | null;
  roundOffAccountId?: string | null;
  roundOffForOpeningAccountId?: string | null;
  defaultWarehouseId?: string | null;
  sampleRetentionWarehouseId?: string | null;
  defaultInTransitWarehouseId?: string | null;
  defaultWarehouseForSalesReturnId?: string | null;
  defaultWipWarehouseId?: string | null;
  defaultFgWarehouseId?: string | null;
  defaultScrapWarehouseId?: string | null;
  bookAdvancePaymentsInSeparatePartyAccount?: boolean;
  defaultAdvanceReceivedAccountId?: string | null;
  defaultAdvancePaidAccountId?: string | null;
}

export interface UpdateEmailDigestSettingsDto {
  companyId?: string;
  isEnabled?: boolean;
  frequency?: EmailDigestFrequency;
  recipients?: string;
  includeOpenSalesOrders?: boolean;
  includeOverdueInvoices?: boolean;
  includeLowStockItems?: boolean;
}

export interface UpdateEmailTemplateDto {
  subject?: string;
  body?: string;
  documentType?: string | null;
}
