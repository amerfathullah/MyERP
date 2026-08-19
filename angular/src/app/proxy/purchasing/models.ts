import type { ScorecardPeriodType } from './scorecard-period-type.enum';
import type { SupplierHoldType } from './supplier-hold-type.enum';
import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { SubcontractingOrderStatus } from './subcontracting-order-status.enum';
import type { AnalyticsGroupBy } from '../sales/analytics-group-by.enum';
import type { AnalyticsPeriodType } from '../sales/analytics-period-type.enum';
import type { SubcontractingInwardOrderStatus } from './subcontracting-inward-order-status.enum';
import type { SubcontractingReceiptStatus } from './subcontracting-receipt-status.enum';

export interface ComparisonItemDto {
  itemId?: string;
  itemDescription?: string;
  supplierPrices?: ComparisonPriceDto[];
  lowestRate?: number;
}

export interface ComparisonPriceDto {
  supplierId?: string;
  quotationId?: string;
  rate?: number;
  quantity?: number;
  amount?: number;
  leadTimeDays?: number | null;
  isQuoted?: boolean;
  isLowestPrice?: boolean;
}

export interface ComparisonSupplierDto {
  supplierId?: string;
  supplierName?: string;
  quotationId?: string;
  quotationNumber?: string | null;
  currency?: string | null;
  validTill?: string | null;
  grandTotal?: number;
}

export interface CreateCriterionDto {
  name?: string;
  weight?: number;
  maxScore?: number;
  formula?: string | null;
}

export interface CreatePurchaseInvoiceDto {
  companyId: string;
  supplierId: string;
  issueDate: string;
  dueDate?: string | null;
  paymentTermsTemplateId?: string | null;
  supplierInvoiceNumber?: string | null;
  currencyCode?: string;
  notes?: string | null;
  costCenterId?: string | null;
  projectId?: string | null;
  isOpening?: boolean;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  updateStock?: boolean;
  warehouseId?: string | null;
  items: CreatePurchaseInvoiceItemDto[];
}

export interface CreatePurchaseInvoiceItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreatePurchaseOrderDto {
  companyId: string;
  supplierId: string;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  costCenterId?: string | null;
  projectId?: string | null;
  notes?: string | null;
  items: CreatePurchaseOrderItemDto[];
}

export interface CreatePurchaseOrderItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
  warehouseId?: string | null;
  expectedDeliveryDate?: string | null;
}

export interface CreatePurchaseOrdersFromMrDto {
  materialRequestId: string;
  items: SupplierSelectionItemDto[];
}

export interface CreatePurchaseReceiptDto {
  companyId: string;
  supplierId: string;
  warehouseId: string;
  postingDate: string;
  purchaseOrderId?: string | null;
  supplierDeliveryNote?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  notes?: string | null;
  items: CreatePurchaseReceiptItemDto[];
}

export interface CreatePurchaseReceiptItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
  purchaseOrderItemId?: string | null;
}

export interface CreateRfqDto {
  companyId?: string;
  transactionDate?: string;
  currencyCode?: string | null;
  messageForSupplier?: string | null;
  items?: CreateRfqItemDto[];
  suppliers?: CreateRfqSupplierDto[];
}

export interface CreateRfqItemDto {
  itemId?: string;
  description?: string;
  qty?: number;
  uom?: string;
}

export interface CreateRfqSupplierDto {
  supplierId?: string;
  email?: string | null;
}

export interface CreateSQItemDto {
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
}

export interface CreateScioItemDto {
  itemId: string;
  bomId?: string | null;
  quantity?: number;
  rate?: number;
  warehouseId?: string | null;
  serviceCostPerQty?: number;
}

export interface CreateScoItemDto {
  itemId: string;
  itemName: string;
  qty?: number;
  rate?: number;
  bomId?: string | null;
  warehouseId?: string | null;
}

export interface CreateScorecardDto {
  supplierId?: string;
  companyId?: string;
  periodType?: ScorecardPeriodType;
  weightingFunction?: string | null;
  standings?: CreateStandingDto[];
  criteria?: CreateCriterionDto[];
}

export interface CreateScorecardPeriodDto {
  startDate?: string;
  endDate?: string;
  score?: number;
}

export interface CreateScrItemDto {
  itemId: string;
  itemName: string;
  qty?: number;
  rate?: number;
  warehouseId?: string | null;
}

export interface CreateStandingDto {
  name?: string;
  minScore?: number;
  maxScore?: number;
  preventPos?: boolean;
  preventRfqs?: boolean;
  warnPos?: boolean;
  warnRfqs?: boolean;
}

export interface CreateSubcontractingInwardOrderDto {
  companyId: string;
  supplierId: string;
  orderDate: string;
  salesOrderId?: string | null;
  subcontractingOrderId?: string | null;
  currencyCode?: string;
  items?: CreateScioItemDto[];
}

export interface CreateSubcontractingOrderDto {
  companyId: string;
  supplierId: string;
  orderDate: string;
  purchaseOrderId?: string | null;
  notes?: string | null;
  items?: CreateScoItemDto[];
}

export interface CreateSubcontractingReceiptDto {
  companyId: string;
  supplierId: string;
  subcontractingOrderId: string;
  postingDate: string;
  warehouseId?: string | null;
  items?: CreateScrItemDto[];
}

export interface CreateSupplierQuotationDto {
  companyId?: string;
  supplierId?: string;
  supplierName?: string | null;
  transactionDate?: string;
  validTill?: string | null;
  currency?: string;
  requestForQuotationId?: string | null;
  items?: CreateSQItemDto[];
}

export interface CreateUpdateIncotermDto {
  code: string;
  title: string;
  description?: string | null;
  isActive?: boolean;
}

export interface CreateUpdateSubcontractingBomDto {
  isActive?: boolean;
  finishedGoodId?: string;
  finishedGoodQty?: number;
  finishedGoodBomId?: string;
  serviceItemId?: string;
  serviceItemQty?: number;
}

export interface CreateUpdateSupplierDto {
  companyId: string;
  name: string;
  supplierCode?: string | null;
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
  defaultPayableAccountId?: string | null;
  isActive?: boolean;
  holdType?: SupplierHoldType;
  preventPurchaseOrders?: boolean;
  preventRfqs?: boolean;
  representsCompanyId?: string | null;
  supplierGroupId?: string | null;
  taxWithholdingCategory?: string | null;
  defaultPaymentTermsTemplateId?: string | null;
  restrictToCompanies?: boolean;
}

export interface CreatedPurchaseOrderInfo {
  purchaseOrderId?: string;
  orderNumber?: string | null;
  supplierName?: string | null;
  itemCount?: number;
  totalAmount?: number;
}

export interface DeliveryPerformanceReportDto {
  suppliers?: SupplierDeliveryPerformanceDto[];
  totalOrders?: number;
  totalOnTime?: number;
  totalLate?: number;
  totalPending?: number;
  overallOnTimeRate?: number;
  overallAvgDelayDays?: number;
}

export interface DropShipDeliveryItemDto {
  purchaseOrderItemId: string;
  qtyChange: number;
}

export interface DuplicateInvoiceCheckResultDto {
  isDuplicate?: boolean;
  existingInvoiceId?: string | null;
  existingInvoiceNumber?: string | null;
  existingInvoiceDate?: string | null;
  existingInvoiceAmount?: number | null;
}

export interface GetScoListDto extends PagedAndSortedResultRequestDto {
  status?: SubcontractingOrderStatus | null;
  companyId?: string | null;
}

export interface GetSupplierListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface IncotermDto extends FullAuditedEntityDto<string> {
  code?: string;
  title?: string;
  description?: string | null;
  isActive?: boolean;
}

export interface InvoicePaymentDto {
  id?: string;
  paymentNumber?: string;
  postingDate?: string;
  amount?: number;
  status?: string;
}

export interface PendingMaterialRequestItemDto {
  materialRequestId?: string;
  materialRequestNumber?: string;
  requestDate?: string;
  requiredByDate?: string | null;
  materialRequestItemId?: string;
  itemId?: string;
  itemName?: string;
  pendingQty?: number;
  uom?: string;
  warehouseId?: string | null;
}

export interface PurchaseAnalyticsReportDto {
  periodLabels?: string[];
  rows?: PurchaseAnalyticsRowDto[];
  grandTotal?: number;
  periodTotals?: number[];
}

export interface PurchaseAnalyticsRequestDto {
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  groupBy?: AnalyticsGroupBy;
  periodType?: AnalyticsPeriodType;
  valueField?: string | null;
}

export interface PurchaseAnalyticsRowDto {
  entityId?: string;
  entityName?: string;
  periodValues?: number[];
  total?: number;
  growth?: number;
}

export interface PurchaseInvoiceDto extends EntityDto<string> {
  companyId?: string;
  invoiceNumber?: string;
  supplierInvoiceNumber?: string | null;
  issueDate?: string;
  dueDate?: string | null;
  supplierId?: string;
  supplierName?: string | null;
  supplierTin?: string | null;
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
  eInvoiceStatus?: string;
  lhdnUuid?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  amendedFromId?: string | null;
  amendmentIndex?: number;
  creditToAccountId?: string;
  daysOverdue?: number;
  isOverdue?: boolean;
  matchingStatus?: string | null;
  isReadyForPayment?: boolean;
  onHold?: boolean;
  items?: PurchaseInvoiceItemDto[];
}

export interface PurchaseInvoiceItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  purchaseOrderItemId?: string | null;
  purchaseReceiptItemId?: string | null;
}

export interface PurchaseInvoiceListSummaryDto {
  totalPayable?: number;
  overdueCount?: number;
  overdueAmount?: number;
  monthlySpend?: number;
  monthlyInvoiceCount?: number;
  postedInvoiceCount?: number;
}

export interface PurchaseOrderDto extends EntityDto<string> {
  companyId?: string;
  orderNumber?: string;
  orderDate?: string;
  expectedDeliveryDate?: string | null;
  supplierId?: string;
  supplierName?: string | null;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  status?: string;
  perReceived?: number;
  perBilled?: number;
  advancePaid?: number;
  perAdvancePaid?: number;
  notes?: string | null;
  supplierConfirmationNumber?: string | null;
  supplierConfirmationDate?: string | null;
  supplierPromisedDate?: string | null;
  isSupplierConfirmed?: boolean;
  items?: PurchaseOrderItemDto[];
}

export interface PurchaseOrderItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  receivedQty?: number;
  billedQty?: number;
  warehouseId?: string | null;
  expectedDeliveryDate?: string | null;
}

export interface PurchaseOrderTrackingBoardDto {
  ordered?: TrackingBoardCardDto[];
  partiallyReceived?: TrackingBoardCardDto[];
  fullyReceived?: TrackingBoardCardDto[];
  completed?: TrackingBoardCardDto[];
  totalOrders?: number;
  overdueCount?: number;
  totalValue?: number;
}

export interface PurchaseReceiptDto extends EntityDto<string> {
  companyId?: string;
  receiptNumber?: string;
  postingDate?: string;
  supplierId?: string;
  supplierName?: string | null;
  purchaseOrderId?: string | null;
  warehouseId?: string;
  warehouseName?: string | null;
  supplierDeliveryNote?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  status?: string;
  perBilled?: number;
  items?: PurchaseReceiptItemDto[];
}

export interface PurchaseReceiptItemDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  billedQty?: number;
  purchaseOrderItemId?: string | null;
}

export interface PurchaseRegisterLineDto {
  invoiceId?: string;
  invoiceNumber?: string;
  postingDate?: string;
  supplierId?: string;
  supplierName?: string | null;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountPaid?: number;
  outstanding?: number;
  isReturn?: boolean;
}

export interface PutawayAllocationResultDto {
  itemId?: string;
  warehouseId?: string;
  qty?: number;
  isUnallocated?: boolean;
}

export interface PutawayItemInput {
  itemId?: string;
  qty?: number;
}

export interface RecordSupplierConfirmationDto {
  confirmationNumber?: string | null;
  confirmationDate?: string | null;
  promisedDeliveryDate?: string | null;
}

export interface RfqDto extends EntityDto<string> {
  companyId?: string;
  rfqNumber?: string;
  transactionDate?: string;
  currencyCode?: string;
  messageForSupplier?: string | null;
  status?: string;
  items?: RfqItemDto[];
  suppliers?: RfqSupplierDto[];
}

export interface RfqItemDto extends EntityDto<string> {
  itemId?: string;
  description?: string;
  qty?: number;
  uom?: string;
}

export interface RfqSupplierDto extends EntityDto<string> {
  supplierId?: string;
  supplierName?: string;
  email?: string | null;
  emailSent?: boolean;
  quoteStatus?: string;
}

export interface RmTransferResultDto {
  stockEntryId?: string;
  entryNumber?: string;
  itemCount?: number;
  totalQty?: number;
}

export interface ScoItemDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
  receivedQty?: number;
}

export interface ScorecardCriterionDto extends EntityDto<string> {
  name?: string;
  weight?: number;
  maxScore?: number;
  formula?: string | null;
}

export interface ScorecardDto extends EntityDto<string> {
  supplierId?: string;
  companyId?: string;
  periodType?: string;
  score?: number;
  currentStanding?: string | null;
  weightingFunction?: string | null;
  standings?: ScorecardStandingDto[];
  criteria?: ScorecardCriterionDto[];
}

export interface ScorecardStandingDto extends EntityDto<string> {
  name?: string;
  minScore?: number;
  maxScore?: number;
  preventPos?: boolean;
  preventRfqs?: boolean;
  warnPos?: boolean;
  warnRfqs?: boolean;
}

export interface SubcontractingBomDto extends EntityDto<string> {
  isActive?: boolean;
  finishedGoodId?: string;
  finishedGoodName?: string | null;
  finishedGoodQty?: number;
  finishedGoodBomId?: string;
  finishedGoodUom?: string | null;
  serviceItemId?: string;
  serviceItemName?: string | null;
  serviceItemQty?: number;
  serviceItemUom?: string | null;
  conversionFactor?: number;
}

export interface SubcontractingInwardOrderDto extends EntityDto<string> {
  companyId?: string;
  orderNumber?: string;
  orderDate?: string;
  supplierId?: string;
  salesOrderId?: string | null;
  subcontractingOrderId?: string | null;
  currencyCode?: string;
  netTotal?: number;
  grandTotal?: number;
  status?: SubcontractingInwardOrderStatus;
  perReceived?: number;
  perBilled?: number;
  items?: SubcontractingInwardOrderItemDto[];
}

export interface SubcontractingInwardOrderItemDto extends EntityDto<string> {
  itemId?: string;
  bomId?: string | null;
  quantity?: number;
  rate?: number;
  amount?: number;
  receivedQty?: number;
  billedQty?: number;
  pendingReceiptQty?: number;
  warehouseId?: string | null;
  serviceCostPerQty?: number;
}

export interface SubcontractingOrderDto extends AuditedEntityDto<string> {
  orderNumber?: string;
  orderDate?: string;
  supplierId?: string;
  supplierName?: string | null;
  companyId?: string;
  netTotal?: number;
  grandTotal?: number;
  status?: SubcontractingOrderStatus;
  perReceived?: number;
  supplierWarehouseId?: string | null;
  items?: ScoItemDto[];
}

export interface SubcontractingReceiptDto extends AuditedEntityDto<string> {
  receiptNumber?: string;
  postingDate?: string;
  supplierId?: string;
  subcontractingOrderId?: string;
  netTotal?: number;
  status?: SubcontractingReceiptStatus;
  items?: SubcontractingReceiptItemDto[];
}

export interface SubcontractingReceiptItemDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
  amount?: number;
  warehouseId?: string | null;
}

export interface SupplierDeliveryPerformanceDto {
  supplierId?: string;
  supplierName?: string;
  totalOrders?: number;
  onTimeDeliveries?: number;
  lateDeliveries?: number;
  pendingDeliveries?: number;
  onTimeRate?: number;
  avgDelayDays?: number;
  totalOrderValue?: number;
}

export interface SupplierDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  name?: string;
  supplierCode?: string | null;
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
  defaultPayableAccountId?: string | null;
  isActive?: boolean;
  holdType?: SupplierHoldType;
  preventPurchaseOrders?: boolean;
  preventRfqs?: boolean;
  representsCompanyId?: string | null;
  supplierGroupId?: string | null;
  taxWithholdingCategory?: string | null;
  defaultPaymentTermsTemplateId?: string | null;
  restrictToCompanies?: boolean;
}

export interface SupplierPaymentLineDto {
  supplierId?: string;
  supplierName?: string;
  invoiceCount?: number;
  totalInvoiced?: number;
  totalPaid?: number;
  totalOutstanding?: number;
  overdueCount?: number;
  overdueAmount?: number;
  paymentTimeliness?: number;
}

export interface SupplierPaymentSummaryReportDto {
  items?: SupplierPaymentLineDto[];
  totalInvoiced?: number;
  totalPaid?: number;
  totalOutstanding?: number;
  totalOverdueAmount?: number;
  supplierCount?: number;
}

export interface SupplierQuotationComparisonDto {
  rfqId?: string | null;
  suppliers?: ComparisonSupplierDto[];
  items?: ComparisonItemDto[];
  lowestTotalAmount?: number;
}

export interface SupplierQuotationDto extends EntityDto<string> {
  companyId?: string;
  supplierId?: string;
  supplierName?: string | null;
  quotationNumber?: string | null;
  transactionDate?: string;
  validTill?: string | null;
  currency?: string;
  netTotal?: number;
  grandTotal?: number;
  status?: number;
  items?: SupplierQuotationItemDto[];
}

export interface SupplierQuotationItemDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
  amount?: number;
}

export interface SupplierSelectionItemDto {
  materialRequestItemId: string;
  supplierId: string;
  quantity?: number;
}

export interface SupplierSelectionResultDto {
  purchaseOrders?: CreatedPurchaseOrderInfo[];
  totalItemsOrdered?: number;
}

export interface TaxWithholdingEntryDto {
  id?: string;
  taxCategory?: string | null;
  withholdingRate?: number;
  taxableAmount?: number;
  withheldAmount?: number;
  postingDate?: string;
  hasLDC?: boolean;
  ldcRate?: number | null;
  certificateNumber?: string | null;
  status?: string | null;
}

export interface ThreeWayMatchingItemDto {
  piItemId?: string;
  itemDescription?: string;
  billedQty?: number;
  billedRate?: number;
  orderedQty?: number | null;
  orderedRate?: number | null;
  receivedQty?: number | null;
  qtyVariance?: number | null;
  rateVariance?: number | null;
  matchLevel?: string;
  hasQtyDiscrepancy?: boolean;
  hasRateDiscrepancy?: boolean;
}

export interface TrackingBoardCardDto {
  orderId?: string;
  orderNumber?: string;
  supplierName?: string;
  orderDate?: string;
  expectedDate?: string | null;
  grandTotal?: number;
  perReceived?: number;
  perBilled?: number;
  stage?: string;
  isOverdue?: boolean;
  daysOverdue?: number;
  itemCount?: number;
}

export interface UnbilledPurchaseOrderItemDto {
  purchaseOrderId?: string;
  orderNumber?: string | null;
  orderDate?: string;
  itemId?: string;
  itemName?: string | null;
  quantity?: number;
  rate?: number;
  uom?: string | null;
  purchaseOrderItemId?: string;
}

export interface UnbilledPurchaseReceiptItemDto {
  purchaseReceiptId?: string;
  receiptNumber?: string | null;
  receiptDate?: string;
  itemId?: string;
  itemName?: string | null;
  quantity?: number;
  rate?: number;
  uom?: string | null;
  purchaseReceiptItemId?: string;
  purchaseOrderItemId?: string | null;
  warehouseId?: string | null;
}

export interface UnbilledReceiptItemDto {
  purchaseReceiptId?: string;
  receiptNumber?: string | null;
  receiptDate?: string;
  itemId?: string;
  itemName?: string | null;
  quantity?: number;
  rate?: number;
  uom?: string | null;
  purchaseReceiptItemId?: string;
  purchaseOrderItemId?: string | null;
}

export interface UpdateDropShipDeliveredQtyDto {
  items: DropShipDeliveryItemDto[];
}

export interface UpdateOrderItemDto {
  itemId: string;
  quantity: number;
  unitPrice: number;
  deliveryDate?: string | null;
  warehouseId?: string | null;
}

export interface UpdateOrderItemsDto {
  items: UpdateOrderItemDto[];
}

export interface UpdateOrderItemsResultDto {
  itemsUpdated?: number;
  newGrandTotal?: number;
  previousGrandTotal?: number;
  warnings?: string[];
}
