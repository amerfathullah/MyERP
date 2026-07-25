import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AuthorizationBasedOn } from './authorization-based-on.enum';
import type { RepeatFrequency } from './repeat-frequency.enum';
import type { RepeatDayOfWeek } from './repeat-day-of-week.enum';

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

export interface AuthorizationRuleDto {
  id?: string;
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

export interface AutoRepeatDto {
  id?: string;
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

export interface ContactDto extends EntityDto<string> {
  partyType?: string;
  partyId?: string;
  fullName?: string;
  email?: string | null;
  phone?: string | null;
  designation?: string | null;
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
  fullName?: string;
  email?: string | null;
  phone?: string | null;
  designation?: string | null;
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

export interface DocumentActivityLogDto {
  id?: string;
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

export interface EmailTemplateDto {
  id?: string;
  name?: string;
  subject?: string;
  body?: string;
  documentType?: string | null;
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

export interface GetNotificationLogListDto extends PagedAndSortedResultRequestDto {
  channel?: string | null;
  status?: string | null;
  documentType?: string | null;
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

export interface NotificationLogDto {
  id?: string;
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

export interface PaymentTermsLookupDto {
  id?: string;
  name?: string;
}

export interface RenderedTemplateDto {
  subject?: string;
  body?: string;
}

export interface RevenueTrendDto {
  month?: string;
  amount?: number;
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
  overDeliveryAllowance?: number;
  overBillingAllowance?: number;
  defaultReceivableAccountId?: string | null;
  defaultPayableAccountId?: string | null;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  defaultBankAccountId?: string | null;
  defaultInventoryAccountId?: string | null;
  depreciationExpenseAccountId?: string | null;
  accumulatedDepreciationAccountId?: string | null;
  exchangeGainLossAccountId?: string | null;
}

export interface UpdateEmailTemplateDto {
  subject?: string;
  body?: string;
  documentType?: string | null;
}
