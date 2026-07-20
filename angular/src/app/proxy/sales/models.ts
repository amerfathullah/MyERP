import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { PricingRuleApplyOn } from './pricing-rule-apply-on.enum';
import type { PricingRuleType } from './pricing-rule-type.enum';
import type { ShippingCalculationMode } from './shipping-calculation-mode.enum';
import type { ShippingRuleType } from './shipping-rule-type.enum';

export interface ApplyPricingRuleDto {
  itemId?: string | null;
  itemGroupId?: string | null;
  qty?: number;
  amount?: number;
  transactionDate?: string;
}

export interface BlanketOrderDto extends EntityDto<string> {
  companyId?: string;
  orderNumber?: string;
  orderType?: string;
  partyId?: string;
  partyName?: string | null;
  fromDate?: string;
  toDate?: string;
  status?: number;
  items?: BlanketOrderItemDto[];
  creationTime?: string;
}

export interface BlanketOrderItemDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
  orderedQty?: number;
  remainingQty?: number;
}

export interface CreateBlanketOrderDto {
  companyId?: string;
  orderType?: string;
  partyId?: string;
  partyName?: string | null;
  fromDate?: string;
  toDate?: string;
  items?: CreateBlanketOrderItemDto[];
}

export interface CreateBlanketOrderItemDto {
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
}

export interface CreateDeliveryNoteDto {
  companyId: string;
  customerId: string;
  warehouseId: string;
  postingDate: string;
  salesOrderId?: string | null;
  shippingAddress?: string | null;
  transporter?: string | null;
  trackingNumber?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  notes?: string | null;
  items: CreateDeliveryNoteItemDto[];
}

export interface CreateDeliveryNoteItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
  salesOrderItemId?: string | null;
}

export interface CreateDunningDto {
  companyId?: string;
  customerId?: string;
  customerName?: string | null;
  postingDate?: string;
  dunningLevel?: number;
  dunningFee?: number;
  interestAmount?: number;
  overduePayments?: CreateDunningOverdueDto[];
}

export interface CreateDunningOverdueDto {
  salesInvoiceId?: string;
  outstandingAmount?: number;
  dueDate?: string;
  overdueDays?: number;
}

export interface CreateInstallationNoteDto {
  companyId?: string;
  customerId?: string;
  deliveryNoteId?: string;
  installationDate?: string;
  items?: InstallationNoteItemDto[];
}

export interface CreateLoyaltyProgramDto {
  companyId?: string;
  name?: string;
  conversionFactor?: number;
  expiryDurationDays?: number;
  expenseAccountId?: string | null;
  costCenterId?: string | null;
  tiers?: LoyaltyProgramTierDto[];
}

export interface CreatePosClosingDto {
  companyId?: string;
  posProfileId?: string;
  posOpeningEntryId?: string;
  userId?: string;
  invoices?: CreatePosClosingInvoiceDto[];
  payments?: CreatePosClosingPaymentDto[];
}

export interface CreatePosClosingInvoiceDto {
  posInvoiceId?: string;
  invoiceNumber?: string;
  grandTotal?: number;
}

export interface CreatePosClosingPaymentDto {
  modeOfPaymentId?: string;
  modeName?: string;
  expectedAmount?: number;
  closingAmount?: number;
}

export interface CreatePosInvoiceDto {
  companyId: string;
  customerId?: string | null;
  warehouseId?: string | null;
  items: PosLineItemDto[];
  paymentMethod?: string;
  amountReceived?: number;
}

export interface CreatePricingRuleDto {
  title?: string;
  applicableFor?: string;
  applyOn?: PricingRuleApplyOn;
  applyOnId?: string | null;
  applyOnName?: string | null;
  ruleType?: PricingRuleType;
  discountPercentage?: number;
  discountAmount?: number;
  rate?: number;
  minQty?: number;
  maxQty?: number;
  minAmount?: number;
  maxAmount?: number;
  priority?: number;
  validFrom?: string | null;
  validUpto?: string | null;
  companyId?: string | null;
}

export interface CreateProductBundleDto {
  itemId?: string;
  itemName?: string | null;
  description?: string | null;
  items?: CreateProductBundleItemDto[];
}

export interface CreateProductBundleItemDto {
  componentItemId?: string;
  itemName?: string | null;
  qty?: number;
}

export interface CreateQuotationDto {
  companyId: string;
  customerId: string;
  issueDate: string;
  validUntil?: string | null;
  currencyCode?: string;
  terms?: string | null;
  notes?: string | null;
  items: CreateQuotationItemDto[];
}

export interface CreateQuotationItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreateSalesInvoiceDto {
  companyId: string;
  customerId: string;
  issueDate: string;
  dueDate?: string | null;
  currencyCode?: string;
  notes?: string | null;
  paymentTermsTemplateId?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  isOpening?: boolean;
  projectId?: string | null;
  updateStock?: boolean;
  warehouseId?: string | null;
  items: CreateSalesInvoiceItemDto[];
}

export interface CreateSalesInvoiceItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreateSalesOrderDto {
  companyId: string;
  customerId: string;
  orderDate: string;
  deliveryDate?: string | null;
  customerPoNumber?: string | null;
  currencyCode?: string;
  terms?: string | null;
  notes?: string | null;
  quotationId?: string | null;
  shippingCountry?: string | null;
  items: CreateSalesOrderItemDto[];
}

export interface CreateSalesOrderItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreateSalesPersonDto {
  name?: string;
  parentSalesPersonId?: string | null;
  isGroup?: boolean;
  employeeId?: string | null;
  commissionRate?: number;
}

export interface CreateSalesTargetDto {
  fiscalYearId?: string | null;
  targetQty?: number;
  targetAmount?: number;
}

export interface CreateShippingRuleDto {
  label?: string;
  companyId?: string;
  accountId?: string;
  calculationMode?: ShippingCalculationMode;
  ruleType?: ShippingRuleType;
  fixedAmount?: number;
  isEnabled?: boolean;
  conditions?: ShippingConditionDto[];
  countries?: string[];
}

export interface CreateSubscriptionDto {
  companyId?: string;
  partyId?: string;
  partyType?: string;
  partyName?: string | null;
  billingInterval?: string;
  billingIntervalCount?: number;
  startDate?: string;
  endDate?: string | null;
  trialPeriodDays?: number;
  plans?: CreateSubscriptionPlanDto[];
}

export interface CreateSubscriptionPlanDto {
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
}

export interface CreateUpdateCustomerDto {
  companyId: string;
  name: string;
  customerCode?: string | null;
  tin?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  idType?: string | null;
  idValue?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  defaultReceivableAccountId?: string | null;
  isActive?: boolean;
}

export interface CustomerDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  name?: string;
  customerCode?: string | null;
  tin?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  idType?: string | null;
  idValue?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  defaultReceivableAccountId?: string | null;
  isActive?: boolean;
}

export interface CustomerRevenueLineDto {
  customerId?: string;
  customerName?: string;
  invoiceCount?: number;
  totalRevenue?: number;
  totalPaid?: number;
  totalOutstanding?: number;
}

export interface CustomerRevenueReportDto {
  items?: CustomerRevenueLineDto[];
  totalRevenue?: number;
  totalOutstanding?: number;
  customerCount?: number;
  fromDate?: string;
  toDate?: string;
}

export interface DeliveryNoteDto extends EntityDto<string> {
  companyId?: string;
  deliveryNumber?: string;
  postingDate?: string;
  customerId?: string;
  salesOrderId?: string | null;
  warehouseId?: string;
  shippingAddress?: string | null;
  transporter?: string | null;
  trackingNumber?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  status?: string;
  items?: DeliveryNoteItemDto[];
}

export interface DeliveryNoteItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  salesOrderItemId?: string | null;
}

export interface DunningDto extends EntityDto<string> {
  companyId?: string;
  customerId?: string;
  customerName?: string | null;
  postingDate?: string;
  dunningLevel?: number;
  totalOutstanding?: number;
  dunningFee?: number;
  interestAmount?: number;
  grandTotal?: number;
  status?: number;
  overduePaymentCount?: number;
}

export interface GeneratedInvoiceDto {
  invoiceId?: string;
  invoiceNumber?: string | null;
  grandTotal?: number;
  periodStart?: string | null;
  periodEnd?: string | null;
}

export interface GetCustomerListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GrossProfitLineDto {
  invoiceId?: string;
  invoiceNumber?: string;
  issueDate?: string;
  customerName?: string | null;
  revenue?: number;
  cost?: number;
  grossProfit?: number;
  grossProfitPercentage?: number;
}

export interface GrossProfitReportDto {
  totalRevenue?: number;
  totalCost?: number;
  grossProfit?: number;
  grossProfitPercentage?: number;
  items?: GrossProfitLineDto[];
}

export interface GrossProfitRequestDto {
  companyId?: string;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface InstallationNoteDto {
  id?: string;
  installationNumber?: string;
  companyId?: string;
  customerId?: string;
  deliveryNoteId?: string;
  installationDate?: string;
  status?: string;
  items?: InstallationNoteItemDto[];
}

export interface InstallationNoteItemDto {
  itemId?: string;
  qty?: number;
  serialNo?: string | null;
}

export interface ItemSalesLineDto {
  itemId?: string;
  itemName?: string;
  totalQty?: number;
  totalRevenue?: number;
  averageRate?: number;
  invoiceCount?: number;
}

export interface ItemSalesReportDto {
  items?: ItemSalesLineDto[];
  totalRevenue?: number;
  totalQty?: number;
  uniqueItems?: number;
  fromDate?: string;
  toDate?: string;
}

export interface LoyaltyBalanceDto {
  customerId?: string;
  programId?: string;
  programName?: string;
  availablePoints?: number;
  currentTier?: string | null;
  redemptionValue?: number;
}

export interface LoyaltyPointEntryDto {
  id?: string;
  points?: number;
  postingDate?: string;
  expiryDate?: string | null;
  invoiceType?: string | null;
  invoiceId?: string | null;
  tierName?: string | null;
  isExpired?: boolean;
  isEarning?: boolean;
}

export interface LoyaltyProgramDto {
  id?: string;
  companyId?: string;
  name?: string;
  conversionFactor?: number;
  expiryDurationDays?: number;
  isEnabled?: boolean;
  expenseAccountId?: string | null;
  costCenterId?: string | null;
  tiers?: LoyaltyProgramTierDto[];
}

export interface LoyaltyProgramTierDto {
  tierName?: string;
  minSpent?: number;
  collectionFactor?: number;
  redemptionFactor?: number;
}

export interface PaymentScheduleDto {
  id?: string;
  dueDate?: string;
  invoicePortion?: number;
  paymentAmount?: number;
  paidAmount?: number;
  outstanding?: number;
}

export interface PosClosingDto {
  id?: string;
  companyId?: string;
  posProfileId?: string;
  postingDate?: string;
  status?: string;
  grandTotal?: number;
  netTotal?: number;
  totalDifference?: number;
  consolidatedSalesInvoiceId?: string | null;
  payments?: PosClosingPaymentDto[];
  invoices?: PosClosingInvoiceDto[];
}

export interface PosClosingInvoiceDto {
  posInvoiceId?: string;
  invoiceNumber?: string;
  grandTotal?: number;
}

export interface PosClosingPaymentDto {
  modeName?: string;
  expectedAmount?: number;
  closingAmount?: number;
  difference?: number;
}

export interface PosInvoiceDto extends EntityDto<string> {
  invoiceNumber?: string;
  issueDate?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountReceived?: number;
  change?: number;
  status?: string;
}

export interface PosItemDto {
  id?: string;
  itemCode?: string;
  itemName?: string;
  sellingPrice?: number;
  uom?: string;
  barcode?: string | null;
}

export interface PosItemSearchDto {
  search?: string | null;
  maxResultCount?: number;
}

export interface PosLineItemDto {
  itemId: string;
  description?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
}

export interface PricingRuleDto extends EntityDto<string> {
  title?: string;
  applicableFor?: string;
  applyOn?: number;
  applyOnId?: string | null;
  applyOnName?: string | null;
  ruleType?: number;
  discountPercentage?: number;
  discountAmount?: number;
  rate?: number;
  minQty?: number;
  maxQty?: number;
  minAmount?: number;
  maxAmount?: number;
  priority?: number;
  validFrom?: string | null;
  validUpto?: string | null;
  isDisabled?: boolean;
}

export interface PricingRuleResultDto {
  ruleId?: string;
  title?: string;
  ruleType?: number;
  discountPercentage?: number;
  discountAmount?: number;
  rate?: number;
  freeItemId?: string | null;
  freeItemQty?: number;
}

export interface ProductBundleDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string | null;
  description?: string | null;
  isActive?: boolean;
  items?: ProductBundleItemDto[];
}

export interface ProductBundleItemDto {
  id?: string;
  componentItemId?: string;
  itemName?: string | null;
  qty?: number;
}

export interface QuotationDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  quotationNumber?: string;
  issueDate?: string;
  validUntil?: string | null;
  customerId?: string;
  customerName?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  terms?: string | null;
  notes?: string | null;
  status?: string;
  convertedToSalesOrderId?: string | null;
  items?: QuotationItemDto[];
}

export interface QuotationItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
}

export interface RegisterFilterDto {
  companyId?: string;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface RegisterReportDto<T> {
  items?: T[];
  totalNet?: number;
  totalTax?: number;
  totalGrand?: number;
  count?: number;
}

export interface SalesInvoiceDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  invoiceNumber?: string;
  issueDate?: string;
  dueDate?: string | null;
  customerId?: string;
  customerName?: string | null;
  currencyCode?: string;
  exchangeRate?: number;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountPaid?: number;
  outstandingAmount?: number;
  baseNetTotal?: number;
  baseTaxAmount?: number;
  baseGrandTotal?: number;
  baseOutstandingAmount?: number;
  status?: string;
  eInvoiceStatus?: string | null;
  lhdnUuid?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  amendedFromId?: string | null;
  amendmentIndex?: number;
  debitToAccountId?: string;
  items?: SalesInvoiceItemDto[];
}

export interface SalesInvoiceItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
}

export interface SalesOrderDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  orderNumber?: string;
  orderDate?: string;
  deliveryDate?: string | null;
  customerId?: string;
  customerName?: string | null;
  customerPoNumber?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  terms?: string | null;
  notes?: string | null;
  status?: string;
  quotationId?: string | null;
  perDelivered?: number;
  perBilled?: number;
  overdueWarning?: string | null;
  items?: SalesOrderItemDto[];
}

export interface SalesOrderItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  deliveredQty?: number;
  billedQty?: number;
  warehouseId?: string | null;
}

export interface SalesPersonDto {
  id?: string;
  name?: string;
  parentSalesPersonId?: string | null;
  isGroup?: boolean;
  employeeId?: string | null;
  commissionRate?: number;
  isEnabled?: boolean;
  targets?: SalesTargetDto[];
}

export interface SalesRegisterLineDto {
  invoiceId?: string;
  invoiceNumber?: string;
  postingDate?: string;
  customerId?: string;
  customerName?: string | null;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountPaid?: number;
  outstanding?: number;
  isReturn?: boolean;
}

export interface SalesTargetDto {
  fiscalYearId?: string | null;
  targetQty?: number;
  targetAmount?: number;
}

export interface ShippingConditionDto {
  fromValue?: number;
  toValue?: number;
  shippingAmount?: number;
}

export interface ShippingRuleDto {
  id?: string;
  label?: string;
  companyId?: string;
  calculationMode?: string;
  ruleType?: string;
  shippingAmount?: number;
  isEnabled?: boolean;
  conditions?: ShippingConditionDto[];
  countries?: string[];
}

export interface SubscriptionDto extends EntityDto<string> {
  companyId?: string;
  partyId?: string;
  partyType?: string;
  partyName?: string | null;
  subscriptionNumber?: string | null;
  billingInterval?: string;
  billingIntervalCount?: number;
  startDate?: string;
  endDate?: string | null;
  currentInvoiceStart?: string | null;
  currentInvoiceEnd?: string | null;
  totalPerInterval?: number;
  status?: number;
  plans?: SubscriptionPlanDto[];
}

export interface SubscriptionPlanDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
}

export interface UpdateLoyaltyProgramDto {
  name?: string;
  conversionFactor?: number;
  expiryDurationDays?: number;
  isEnabled?: boolean;
  expenseAccountId?: string | null;
  costCenterId?: string | null;
}

export interface UpdateSalesPersonDto {
  parentSalesPersonId?: string | null;
  isGroup?: boolean;
  employeeId?: string | null;
  commissionRate?: number;
}
